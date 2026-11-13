using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SolastaUnfinishedBusiness.Models;
using static SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;
using Plan = SolastaUnfinishedBusiness.Models.CompressedMulticasterProgressionPlan;

// Executes the actual production calculator, without loading Unity or installing Harmony patches.
// Compiled table/isolation assertions are explicitly labelled metadata checks.
internal static class CompressedMulticasterTests
{
    private static readonly int[] FullAccess = { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 9, 9 };
    private static readonly int[] HalfAccess = { 0, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5 };
    private static readonly int[] ThirdAccess = { 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 4, 4 };
    private static readonly int[] PactAccess = { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 };
    private static CastingProfile Cast(string id, CasterKind kind, bool compression = true)
    {
        var circles = kind switch
        {
            CasterKind.None => new int[20], CasterKind.Full => FullAccess, CasterKind.Half => HalfAccess,
            CasterKind.HalfRoundUp => new[] { 1 }.Concat(HalfAccess.Skip(1)).ToArray(),
            CasterKind.OneThird => ThirdAccess, _ => PactAccess
        };
        return new(id, kind, kind == CasterKind.Half ? 2 : kind == CasterKind.OneThird ? 3 : 1, compression, circles);
    }
    private static Plan Registry(bool roundUp = false)
    {
        ClassProfile Full(string id, int choice) => new(id, true, Cast("Cast" + id, CasterKind.Full), choice,
            new[] { new SubclassProfile(id + "Subclass", true) });
        ClassProfile Half(string id) => new(id, true, Cast("Cast" + id, roundUp ? CasterKind.HalfRoundUp : CasterKind.Half), 3,
            new[] { new SubclassProfile(id + "Subclass", true) });
        return new("Stage13.2-source-profile-1", new[]
        {
            Full("Sorcerer", 1), Full("Wizard", 2), Full("Cleric", 1), Full("Bard", 3), Full("Druid", 2),
            Half("Paladin"), Half("Ranger"),
            new ClassProfile("Inventor", true, Cast("CastInventor", CasterKind.HalfRoundUp), 3,
                new[] { new SubclassProfile("InnovationArtillerist", true) }),
            new ClassProfile("Warlock", true, Cast("CastWarlock", CasterKind.Pact), 1,
                new[] { new SubclassProfile("Patron", true) }),
            new ClassProfile("Fighter", true, null, 3, new[]
            {
                new SubclassProfile("Champion", true),
                new SubclassProfile("Spellblade", false, false, Cast("CastSpellblade", CasterKind.OneThird, false)),
                new SubclassProfile("SpellShield", false, false, Cast("CastSpellShield", CasterKind.OneThird, false))
            }),
            new ClassProfile("Rogue", true, null, 3, new[]
            {
                new SubclassProfile("ShadowCaster", true, true, Cast("CastShadowCaster", CasterKind.OneThird, false)),
                new SubclassProfile("ArcaneScoundrel", true, true, Cast("CastArcaneScoundrel", CasterKind.OneThird, false))
            })
        });
    }
    private static ClassInput Class(string id, int actual, bool compression = true, string subclass = null) =>
        new(id, actual, compression, subclass ?? (id switch
        {
            "Fighter" => "Champion", "Rogue" => "ShadowCaster", "Inventor" => "InnovationArtillerist",
            "Warlock" => "Patron", _ => id + "Subclass"
        }));
    private static Snapshot Hero(IEnumerable<ClassInput> classes, IEnumerable<AuxiliaryInput> auxiliary = null)
    {
        var list = classes.ToArray();
        return new(list.Sum(x => x.ActualLevel), list,
            list.SelectMany(x => Enumerable.Repeat(x.ClassId, x.ActualLevel)), auxiliary);
    }
    private static IEnumerable<TypeDefinition> Types(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(Types));
    private static int Integer(Instruction i) => i.OpCode.Code switch
    {
        Code.Ldc_I4_0 => 0, Code.Ldc_I4_1 => 1, Code.Ldc_I4_2 => 2, Code.Ldc_I4_3 => 3, Code.Ldc_I4_4 => 4,
        Code.Ldc_I4_5 => 5, Code.Ldc_I4_6 => 6, Code.Ldc_I4_7 => 7, Code.Ldc_I4_8 => 8,
        Code.Ldc_I4_S => Convert.ToInt32(i.Operand), Code.Ldc_I4 => (int)i.Operand, _ => int.MinValue
    };
    internal static int[] AddedIntegers(TypeDefinition type, string field)
    {
        var body = type.Methods.Single(x => x.IsConstructor && x.IsStatic).Body.Instructions;
        var end = body.ToList().FindIndex(i => i.OpCode.Code == Code.Stsfld && ((FieldReference)i.Operand).Name == field);
        var start = end - 1;
        while (start >= 0 && body[start].OpCode.Code != Code.Stsfld) start--;
        return body.Skip(start + 1).Take(end - start - 1).Where(i => i.Operand is MethodReference m && m.Name == "Add" && m.DeclaringType.Name == "List`1")
            .Select(i => Integer(i.Previous)).ToArray();
    }
    private static bool Reject(Action action)
    {
        try { action(); return false; } catch (ArgumentException) { return true; }
    }
    internal static void Run(Action<bool, string> check, AssemblyDefinition mod)
    {
        var types = mod.MainModule.Types.SelectMany(Types).ToArray();
        var slots = AddedIntegers(types.Single(x => x.Name == "FeatureDefinitionCastSpellBuilder"), "SlotsByCasterLevel");
        var points = AddedIntegers(types.Single(x => x.Name == "ModifyPowerPoolAmountPowerSpellPoints"), "SpellPointsByLevel");
        check(!Plan.MixedGameplayReady && Plan.MaximumResourceIndex == 20, "Multicaster production: calculator ready for tests only; mixed gameplay gate closed");
        check(points.Length == 21 && points[20] == 133 && slots.Length == 210, "Multicaster compiled metadata: actual installed SP0..20 and twenty-one full-caster slot rows");
        check(FullAccess.SequenceEqual(Enumerable.Range(1, 20).Select(n => Array.FindLastIndex(slots.Skip(n * 10).Take(10).ToArray(), x => x > 0) + 1)), "Multicaster compiled metadata: original full spell-access fixture matches installed final table");
        Examples(check, points); MixedSettings(check); Boundaries(check); Validation(check); Immutability(check); Isolation(check, types);
    }
    private static void Examples(Action<bool, string> check, int[] points)
    {
        var plan = Registry();
        void Example(string name, ClassInput[] input, int raw, int expectedPoints,
            params (string Class, int Actual, int Effective, int Casting, int Circle)[] expected)
        {
            var snapshot = Hero(input); var history = snapshot.ActualClassHistory.ToArray();
            var result = plan.Calculate(snapshot);
            check(result.IsValid && result.Input.ActualCharacterLevel == input.Sum(x => x.ActualLevel) && result.Input.ActualClassHistory.SequenceEqual(history), "Multicaster production example: actual levels/history unchanged:" + name);
            check(result.RawSharedCasterLevel == raw && result.ResourceTableIndex == Math.Min(20, raw) && result.StandardSlotRowIndex == Math.Min(20, raw) - 1 && points[result.ResourceTableIndex] == expectedPoints, "Multicaster production example: one bounded standard index/raw/SP:" + name);
            check(result.Saturated == (raw > 20) && result.Diagnostics.All(x => x.Code == DiagnosticCode.ResourceTableSaturated && !x.IsError), "Multicaster production example: saturation diagnostic does not discard class progression:" + name);
            foreach (var entry in expected)
            {
                var c = result.Classes.Single(x => x.ClassId == entry.Class);
                check(c.ActualClassLevel == entry.Actual && c.EffectiveClassLevel == entry.Effective && c.EffectiveSubclassLevel == entry.Effective && c.ActualSubclassLevel == entry.Actual, "Multicaster production example: independent actual/class/subclass levels:" + name + "/" + entry.Class);
                check(c.EffectiveCastingLevel == entry.Casting && c.SpellAccessLevel == entry.Casting && c.MaximumSpellCircle == entry.Circle, "Multicaster production example: independent repertoire access, never shared ceiling:" + name + "/" + entry.Class);
            }
            var reversed = plan.Calculate(new Snapshot(snapshot.ActualCharacterLevel, input.Reverse(), history));
            check(reversed.IsValid && reversed.RawSharedCasterLevel == raw && reversed.Classes.Select(x => (x.ClassId, x.EffectiveCastingLevel, x.MaximumSpellCircle)).SequenceEqual(result.Classes.Select(x => (x.ClassId, x.EffectiveCastingLevel, x.MaximumSpellCircle))), "Multicaster production example: class enumeration order invariant:" + name);
        }
        Example("Sorcerer12C/Wizard8C", new[] { Class("Sorcerer", 12), Class("Wizard", 8) }, 31, 133, ("Sorcerer", 12, 20, 20, 9), ("Wizard", 8, 11, 11, 6));
        Example("Cleric12C/Wizard8C", new[] { Class("Cleric", 12), Class("Wizard", 8) }, 31, 133, ("Cleric", 12, 20, 20, 9), ("Wizard", 8, 11, 11, 6));
        Example("Paladin12C/Sorcerer8C", new[] { Class("Paladin", 12), Class("Sorcerer", 8) }, 21, 133, ("Paladin", 12, 20, 20, 5), ("Sorcerer", 8, 11, 11, 6));
        Example("Ranger12C/Inventor8C", new[] { Class("Ranger", 12), Class("Inventor", 8) }, 16, 94, ("Ranger", 12, 20, 20, 5), ("Inventor", 8, 11, 11, 3));
        Example("Bard8C/Cleric6C/Fighter6C", new[] { Class("Bard", 8), Class("Cleric", 6), Class("Fighter", 6) }, 18, 114, ("Bard", 8, 11, 11, 6), ("Cleric", 6, 7, 7, 4), ("Fighter", 6, 7, 0, 0));
        Example("Warlock12C/Sorcerer8C", new[] { Class("Warlock", 12), Class("Sorcerer", 8) }, 11, 73, ("Warlock", 12, 20, 20, 5), ("Sorcerer", 8, 11, 11, 6));
        Example("Fighter8N-Spellblade/Wizard12C", new[] { Class("Fighter", 8, false, "Spellblade"), Class("Wizard", 12) }, 22, 133, ("Fighter", 8, 8, 8, 2), ("Wizard", 12, 20, 20, 9));
        Example("Wizard12C/Cleric8N", new[] { Class("Wizard", 12), Class("Cleric", 8, false) }, 28, 133, ("Wizard", 12, 20, 20, 9), ("Cleric", 8, 8, 8, 4));
        Example("Wizard6C/Paladin6C", new[] { Class("Wizard", 6), Class("Paladin", 6) }, 10, 64, ("Wizard", 6, 7, 7, 4), ("Paladin", 6, 7, 7, 2));
        Example("Wizard8C/Cleric6C/Sorcerer6C", new[] { Class("Wizard", 8), Class("Cleric", 6), Class("Sorcerer", 6) }, 25, 133, ("Wizard", 8, 11, 11, 6), ("Cleric", 6, 7, 7, 4), ("Sorcerer", 6, 7, 7, 4));
        Example("Wizard8N/Fighter12N", new[] { Class("Wizard", 8, false), Class("Fighter", 12, false) }, 8, 44, ("Wizard", 8, 8, 8, 4), ("Fighter", 12, 12, 0, 0));
        foreach (var subclass in new[] { "ShadowCaster", "ArcaneScoundrel" })
        {
            var r = plan.Calculate(Hero(new[] { Class("Rogue", 8, true, subclass), Class("Wizard", 6) })); var rogue = r.Classes.Single(x => x.ClassId == "Rogue");
            check(r.IsValid && r.RawSharedCasterLevel == 9 && rogue.EffectiveClassLevel == 11 && rogue.EffectiveSubclassLevel == 11 && rogue.EffectiveCastingLevel == 8 && rogue.MaximumSpellCircle == 2, "Multicaster production: Rogue features compressed, third-caster magic stays actual:" + subclass);
        }
        foreach (var subclass in new[] { "Spellblade", "SpellShield" })
        {
            var r = plan.Calculate(Hero(new[] { Class("Fighter", 8, true, subclass), Class("Wizard", 12) }));
            check(!r.IsValid && r.ResourceTableIndex == 0 && r.Diagnostics.Any(x => x.Code == DiagnosticCode.UnsupportedCompressedSubclass), "Multicaster production: existing compressed Fighter caster-subclass restriction retained:" + subclass);
        }
        var w = plan.Calculate(Hero(new[] { Class("Warlock", 12), Class("Sorcerer", 8) }));
        check(w.PactProgressions.Single().EffectiveCastingLevel == 20 && w.PactProgressions.Single().ContributionNumerator == 0 && w.CastingParticipantCount == 2, "Multicaster production: Pact is separate progression, contributes zero standard levels and counts for mixed rounding");
    }
    private static void MixedSettings(Action<bool, string> check)
    {
        var p = Registry();
        var map = new[] { 0, 1, 2, 3, 4, 5, 7, 9, 11, 13, 15, 17, 20 };
        void Matrix(string name, string[] ids, int[] actual, int[] raw)
        {
            for (var mask = 0; mask < raw.Length; mask++)
            {
                var input = ids.Select((id, i) => Class(id, actual[i], (mask & (1 << i)) != 0)).ToArray();
                var r = p.Calculate(Hero(input));
                check(r.IsValid && r.RawSharedCasterLevel == raw[mask] && r.ResourceTableIndex == Math.Min(20, raw[mask]), "Multicaster production settings matrix: independent class switches/raw/bounded resource:" + name + "/" + mask);
                foreach (var c in r.Classes)
                {
                    var original = input.Single(x => x.ClassId == c.ClassId);
                    var expected = original.CompressionEnabled ? map[original.ActualLevel] : original.ActualLevel;
                    check(c.ActualClassLevel == original.ActualLevel && c.EffectiveClassLevel == expected && c.EffectiveSubclassLevel == expected && c.EffectiveCastingLevel == (c.Kind == CasterKind.None ? 0 : expected), "Multicaster production settings matrix: own class/subclass/casting levels:" + name + "/" + mask + "/" + c.ClassId);
                }
            }
        }
        Matrix("Sorcerer12/Wizard8", new[] { "Sorcerer", "Wizard" }, new[] { 12, 8 }, new[] { 20, 28, 23, 31 });
        Matrix("Cleric12/Wizard8", new[] { "Cleric", "Wizard" }, new[] { 12, 8 }, new[] { 20, 28, 23, 31 });
        Matrix("Paladin12/Sorcerer8", new[] { "Paladin", "Sorcerer" }, new[] { 12, 8 }, new[] { 14, 18, 17, 21 });
        Matrix("Ranger12/Inventor8", new[] { "Ranger", "Inventor" }, new[] { 12, 8 }, new[] { 10, 14, 12, 16 });
        Matrix("Bard8/Cleric6/Fighter6", new[] { "Bard", "Cleric", "Fighter" }, new[] { 8, 6, 6 }, new[] { 14, 17, 15, 18, 14, 17, 15, 18 });
        Matrix("Warlock12/Sorcerer8", new[] { "Warlock", "Sorcerer" }, new[] { 12, 8 }, new[] { 8, 8, 11, 11 });
        foreach (var subclass in new[] { "Spellblade", "SpellShield" }) foreach (var wizardCompressed in new[] { false, true })
        {
            var r = p.Calculate(Hero(new[] { Class("Fighter", 8, false, subclass), Class("Wizard", 12, wizardCompressed) }));
            check(r.IsValid && r.ResourceTableIndex == (wizardCompressed ? 20 : 14) && r.Classes.Single(x => x.ClassId == "Fighter").MaximumSpellCircle == 2, "Multicaster production settings matrix: ordinary Fighter casting subclass beside ordinary/compressed Wizard:" + subclass + "/" + wizardCompressed);
        }
        var innate = p.Calculate(new Snapshot(0, Array.Empty<ClassInput>(), Array.Empty<string>(), new[] { new AuxiliaryInput("Race", AuxiliaryRole.Race, 2), new AuxiliaryInput("Feat", AuxiliaryRole.Feat, 1) }));
        check(innate.IsValid && innate.ResourceTableIndex == 0 && innate.StandardSlotRowIndex == null && innate.AuxiliarySources.Count == 2, "Multicaster production: racial/feat-only access preserves no standard or Pact caster");
    }
    private static void Boundaries(Action<bool, string> check)
    {
        var mapping = new[] { 0, 1, 2, 3, 4, 5, 7, 9, 11, 13, 15, 17, 20 };
        foreach (var kind in new[] { CasterKind.Full, CasterKind.Half, CasterKind.HalfRoundUp, CasterKind.OneThird, CasterKind.None, CasterKind.Pact })
        {
            var casting = Cast("CastA", kind); var p = new Plan("boundaries", new[] { new ClassProfile("A", true, casting) });
            var solo = kind switch
            {
                CasterKind.Full => Enumerable.Range(0, 21).ToArray(),
                CasterKind.Half => new[] { 0, 0, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10 },
                CasterKind.HalfRoundUp => new[] { 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10 },
                CasterKind.OneThird => new[] { 0, 0, 0, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 5, 5, 5, 6, 6, 6, 7, 7 },
                _ => new int[21]
            };
            foreach (var compressed in new[] { false, true })
            foreach (var actual in Enumerable.Range(0, compressed ? 13 : 21))
            {
                var snapshot = Hero(new[] { new ClassInput("A", actual, compressed) }); var r = p.Calculate(snapshot); var c = r.Classes.Single(); var e = compressed ? mapping[actual] : actual;
                check(r.IsValid && c.ActualClassLevel == actual && c.EffectiveClassLevel == e && r.Input.ActualCharacterLevel == actual, "Multicaster production boundary: own actual/compressed class mapping:" + kind + "/" + compressed + "/" + actual);
                check(r.RawSharedCasterLevel == solo[e] && r.ResourceTableIndex == solo[e] && r.Mode == RoundingMode.InstalledSolo && r.StandardSlotRowIndex == (solo[e] == 0 ? (int?)null : solo[e] - 1), "Multicaster production boundary: installed solo rounding and safe zero row:" + kind + "/" + compressed + "/" + actual);
                check(c.MaximumSpellCircle == (c.SpellAccessLevel == 0 ? 0 : casting.MaximumSpellCircles[e - 1]) && c.EffectiveCastingLevel == (kind == CasterKind.None ? 0 : e), "Multicaster production boundary: access comes from original own table:" + kind + "/" + compressed + "/" + actual);
            }
        }
        void Mixed(CasterKind aKind, int aLevel, CasterKind bKind, int bLevel, int expected, string name)
        {
            var p = new Plan("rounding", new[] { new ClassProfile("A", true, Cast("CastA", aKind)), new ClassProfile("B", true, Cast("CastB", bKind)) });
            var r = p.Calculate(Hero(new[] { new ClassInput("A", aLevel), new ClassInput("B", bLevel) }));
            check(r.IsValid && r.RawSharedCasterLevel == expected && r.Mode == RoundingMode.UbGroupedMulticlass && r.CastingParticipantCount == 2, "Multicaster production grouped rounding: " + name);
            check(r.Buckets.Sum(x => x.RoundedContribution) == expected && r.Classes.All(x => x.ContributionDenominator == (x.Kind is CasterKind.Half or CasterKind.HalfRoundUp ? 2 : x.Kind == CasterKind.OneThird ? 3 : 1)), "Multicaster production exact contribution trace: " + name);
        }
        Mixed(CasterKind.Half, 3, CasterKind.Half, 3, 3, "Half3+3 rounds bucket, not each class");
        Mixed(CasterKind.OneThird, 4, CasterKind.OneThird, 5, 3, "Third4+5 rounds bucket");
        Mixed(CasterKind.HalfRoundUp, 3, CasterKind.HalfRoundUp, 3, 3, "HU3+3 rounds bucket once");
        Mixed(CasterKind.Half, 3, CasterKind.HalfRoundUp, 3, 3, "Half and HU keep separate buckets");
        Mixed(CasterKind.Half, 3, CasterKind.Pact, 1, 1, "Pact participant changes Half rounding without adding levels");
        Mixed(CasterKind.HalfRoundUp, 3, CasterKind.Pact, 1, 2, "Pact with HU keeps ceil");
        Mixed(CasterKind.OneThird, 4, CasterKind.Pact, 1, 1, "Pact with Third uses floor");
        Mixed(CasterKind.Full, 5, CasterKind.Full, 5, 10, "ordinary full/full");
        Mixed(CasterKind.Full, 6, CasterKind.Pact, 12, 6, "Pact never treated as Full12");
        var aux = new[] { new AuxiliaryInput("RaceSpell", AuxiliaryRole.Race, 2), new AuxiliaryInput("FeatSpell", AuxiliaryRole.Feat, 1), new AuxiliaryInput("Wand", AuxiliaryRole.Item, 5), new AuxiliaryInput("AutoA", AuxiliaryRole.AttachedGrant, 5, "A"), new AuxiliaryInput("AutoB", AuxiliaryRole.AttachedGrant, 5, "A") };
        var half = new Plan("auxiliary", new[] { new ClassProfile("A", true, Cast("CastA", CasterKind.Half)) });
        var withAux = half.Calculate(Hero(new[] { new ClassInput("A", 3) }, aux.Concat(aux.Take(1))));
        check(withAux.IsValid && withAux.ResourceTableIndex == 2 && withAux.CastingParticipantCount == 1 && withAux.AuxiliarySources.Count == 5 && withAux.AuxiliarySources.Single(x => x.SourceId == "Wand").MaximumSpellCircle == 5, "Multicaster production: racial/feat/item/attached views deduplicate, retain access, never change solo rounding");
        var empty = Registry().Calculate(new Snapshot(0, Array.Empty<ClassInput>(), Array.Empty<string>()));
        check(empty.IsValid && empty.ResourceTableIndex == 0 && empty.StandardSlotRowIndex == null && empty.CastingParticipantCount == 0, "Multicaster production: empty new hero has no resource row");
        var martial = Registry().Calculate(Hero(new[] { Class("Fighter", 12) }));
        check(martial.IsValid && martial.RawSharedCasterLevel == 0 && martial.PactProgressions.Count == 0, "Multicaster production: absent caster in compressed martial hero grants no resource");
        var warlock = Registry().Calculate(Hero(new[] { Class("Warlock", 12) }));
        check(warlock.IsValid && warlock.ResourceTableIndex == 0 && warlock.StandardSlotRowIndex == null && warlock.PactProgressions.Single().SpellAccessLevel == 20 && warlock.PactProgressions.Single().MaximumSpellCircle == 5, "Multicaster production: Warlock-only keeps effective20 Pact access with zero standard resource");
        var pending = Registry().Calculate(Hero(new[] { new ClassInput("Fighter", 2) }));
        check(pending.IsValid && pending.Classes.Single().ActualSubclassLevel == 0 && pending.Classes.Single().EffectiveSubclassLevel == 0 && pending.ResourceTableIndex == 0, "Multicaster production: subclass absent before selection means no invented third-caster");
        foreach (var level in new[] { 1, 2 })
        {
            var p = new Plan("unlock", new[] { new ClassProfile("A", true, Cast("Half", CasterKind.Half)), new ClassProfile("B", true, Cast("Full", CasterKind.Full)) });
            var r = p.Calculate(Hero(new[] { new ClassInput("A", level), new ClassInput("B", 1) }));
            check(r.IsValid && r.ResourceTableIndex == level && r.CastingParticipantCount == level, "Multicaster production: Half start-at2 unlock eligibility:" + level);
        }
        var hu = Registry(true).Calculate(Hero(new[] { Class("Ranger", 1, false), Class("Paladin", 1, false) }));
        check(!hu.IsValid && hu.Diagnostics.Any(x => x.Code == DiagnosticCode.PrematureSubclass), "Multicaster production: HU source does not bypass subclass unlock validation");
        hu = Registry(true).Calculate(Hero(new[] { new ClassInput("Ranger", 1), new ClassInput("Paladin", 1) }));
        check(hu.IsValid && hu.CastingParticipantCount == 2 && hu.ResourceTableIndex == 1, "Multicaster production: start-at1 HalfRoundUp final profile participates without a subclass");
        var precedence = new Plan("precedence", new[] { new ClassProfile("A", true, Cast("ClassFull", CasterKind.Full), 1, new[] { new SubclassProfile("Sub", false, true, Cast("SubThird", CasterKind.OneThird, false)) }) });
        var primary = precedence.Calculate(Hero(new[] { new ClassInput("A", 8, true, "Sub") })); var own = primary.Classes.Single();
        check(primary.IsValid && primary.ResourceTableIndex == 11 && own.CastingDefinitionId == "ClassFull" && own.EffectiveSubclassLevel == 8 && own.EffectiveClassLevel == 11, "Multicaster production: installed class-over-subclass caster precedence; feature progression remains independent");
    }
    private static void Validation(Action<bool, string> check)
    {
        var p = Registry();
        void Invalid(Snapshot snapshot, DiagnosticCode code, string name)
        {
            var r = p.Calculate(snapshot);
            check(!r.IsValid && r.ResourceTableIndex == 0 && r.StandardSlotRowIndex == null && r.Classes.Count == 0 && r.Diagnostics.Any(x => x.Code == code && x.IsError), "Multicaster production invalid snapshot: " + name);
        }
        Invalid(Hero(new[] { new ClassInput("Unknown", 1) }), DiagnosticCode.UnsupportedClass, "unknown class is not silently None");
        Invalid(Hero(new[] { Class("Fighter", 8, false, "Unknown") }), DiagnosticCode.UnknownSubclass, "unknown subclass is not silently None");
        Invalid(Hero(new[] { new ClassInput("Fighter", 8) }), DiagnosticCode.MissingSubclass, "missing required casting-subclass selection fails closed");
        Invalid(Hero(new[] { new ClassInput("Wizard", 12, true) }), DiagnosticCode.MissingSubclass, "missing required full-class subclass fails explicitly");
        Invalid(Hero(new[] { Class("Wizard", 13) }), DiagnosticCode.UnsupportedCompressedLevel, "compressed class13 cannot be masked by table clamp");
        Invalid(Hero(new[] { Class("Wizard", 12), Class("Sorcerer", 9) }), DiagnosticCode.InvalidCharacterLevel, "actual total21 rejected before resource calculation");
        Invalid(Hero(new[] { Class("Wizard", 4), Class("Sorcerer", 4), Class("Cleric", 4), Class("Bard", 4) }), DiagnosticCode.TooManyClasses, "four actual classes rejected");
        Invalid(Hero(new[] { Class("Wizard", 4), Class("Wizard", 4) }), DiagnosticCode.DuplicateClass, "duplicate class owner cannot duplicate contribution");
        Invalid(new Snapshot(4, new[] { Class("Wizard", 4) }, new[] { "Wizard", "Wizard", "Cleric", "Wizard" }), DiagnosticCode.HistoryMismatch, "foreign class history rejected");
        Invalid(new Snapshot(3, new[] { Class("Wizard", 4) }, Enumerable.Repeat("Wizard", 3)), DiagnosticCode.HistoryMismatch, "class and character actual levels disagree");
        Invalid(new Snapshot(0, new[] { new ClassInput("Wizard", -1) }, Array.Empty<string>()), DiagnosticCode.InvalidClassLevel, "negative class level");
        Invalid(new Snapshot(0, new[] { new ClassInput("Wizard", 21) }, Array.Empty<string>()), DiagnosticCode.InvalidClassLevel, "class level above20");
        Invalid(new Snapshot(0, new ClassInput[] { null }, Array.Empty<string>()), DiagnosticCode.InvalidClassLevel, "null class entry");
        Invalid(new Snapshot(0, new[] { new ClassInput(" ", 0) }, Array.Empty<string>()), DiagnosticCode.InvalidClassLevel, "blank class owner");
        var a = new[] { Class("Wizard", 8) };
        Invalid(Hero(a, new[] { new AuxiliaryInput("Unknown", AuxiliaryRole.Unsupported) }), DiagnosticCode.UnsupportedSource, "unclassified repertoire rejected");
        Invalid(Hero(a, new[] { new AuxiliaryInput("Bonus", AuxiliaryRole.AttachedGrant, 2) }), DiagnosticCode.AmbiguousOwner, "attached spell source without owner rejected");
        Invalid(Hero(a, new[] { new AuxiliaryInput("Bonus", AuxiliaryRole.AttachedGrant, 2, "Cleric") }), DiagnosticCode.AmbiguousOwner, "attached source cannot guess absent class owner");
        Invalid(Hero(a, new[] { new AuxiliaryInput("Race", AuxiliaryRole.Race, 10) }), DiagnosticCode.UnsupportedSource, "auxiliary circle10 rejected");
        Invalid(Hero(a, new[] { new AuxiliaryInput("Race", AuxiliaryRole.Race, 1), new AuxiliaryInput("Race", AuxiliaryRole.Feat, 1) }), DiagnosticCode.ConflictingSource, "contradictory source identity rejected");
        check(Reject(() => new CastingProfile("Bad", CasterKind.Full, 1, true, new int[19])) && Reject(() => new CastingProfile("Bad", CasterKind.Pact, 1, true, Enumerable.Repeat(9, 20))), "Multicaster production profile validation: incomplete original table and invalid Pact circle rejected");
        check(Reject(() => new CastingProfile(" ", CasterKind.Full, 1, true, FullAccess)) && Reject(() => new CastingProfile("Bad", (CasterKind)100, 1, true, FullAccess)), "Multicaster production profile validation: stable source ID and supported kind required");
        check(Reject(() => new Plan("v", new[] { new ClassProfile("A", true), new ClassProfile("A", true) })) && Reject(() => new Plan(" ", Array.Empty<ClassProfile>())), "Multicaster production registry validation: duplicate owner and missing version rejected");
        var unsupported = new Plan("no-adapter", new[] { new ClassProfile("A", false, Cast("A", CasterKind.Full, false)) });
        var no = unsupported.Calculate(Hero(new[] { new ClassInput("A", 8, true) }));
        check(!no.IsValid && no.Diagnostics.Single().Code == DiagnosticCode.UnsupportedCompression, "Multicaster production: unsupported compression is not silently mapped to effective11");
    }
    private static void Immutability(Action<bool, string> check)
    {
        var rows = FullAccess.ToArray(); var profile = new CastingProfile("CastA", CasterKind.Full, 1, true, rows);
        var registryInput = new[] { new ClassProfile("A", true, profile) }; var p = new Plan("immutable-v1", registryInput);
        var classes = new[] { new ClassInput("A", 8, true) }; var history = Enumerable.Repeat("A", 8).ToArray();
        var aux = new[] { new AuxiliaryInput("Race", AuxiliaryRole.Race, 2) }; var snapshot = new Snapshot(8, classes, history, aux);
        rows[10] = 0; registryInput[0] = new ClassProfile("Changed", false); classes[0] = new ClassInput("Changed", 1); history[0] = "Changed"; aux[0] = new AuxiliaryInput("Changed", AuxiliaryRole.Unsupported);
        var before = p.Calculate(snapshot); var after = p.Calculate(snapshot);
        check(before.IsValid && before.ResourceTableIndex == 11 && before.Classes.Single().MaximumSpellCircle == 6 && before.RegistryVersion == "immutable-v1", "Multicaster production immutable registry: caller changes cannot alter profile/access/version");
        check(snapshot.Classes.Single().ClassId == "A" && snapshot.ActualClassHistory.All(x => x == "A") && snapshot.AuxiliarySources.Single().SourceId == "Race", "Multicaster production immutable snapshot: caller array changes cannot alter actual history/classes/auxiliary grants");
        check(before.Input == snapshot && before.ResourceTableIndex == after.ResourceTableIndex && before.Classes.Single().SpellAccessLevel == after.Classes.Single().SpellAccessLevel && snapshot.Classes.Single().ActualLevel == 8, "Multicaster production: reusable deterministic calculation has no transformation/level-up side effect");
        var immutable = false; try { ((IList<ClassInput>)snapshot.Classes).Add(new ClassInput("Other", 1)); } catch (NotSupportedException) { immutable = true; }
        check(immutable, "Multicaster production: snapshots expose read-only collections");
    }
    private static void Isolation(Action<bool, string> check, TypeDefinition[] types)
    {
        var root = types.Single(x => x.Name == nameof(CompressedMulticasterProgressionPlan)); var own = Types(root).ToArray();
        bool IsOwn(TypeReference t) => t.FullName.StartsWith(root.FullName, StringComparison.Ordinal);
        var externalCalls = types.Where(t => !IsOwn(t)).SelectMany(t => t.Methods).Where(m => m.HasBody)
            .Where(m => !m.DeclaringType.FullName.StartsWith("SolastaUnfinishedBusiness.Models.StandardCasterResourcePlan", StringComparison.Ordinal) &&
                        !m.DeclaringType.FullName.StartsWith("SolastaUnfinishedBusiness.Models.StandardMulticasterResourcesContext", StringComparison.Ordinal) &&
                        !m.DeclaringType.FullName.StartsWith("SolastaUnfinishedBusiness.Models.MultipleSpellRepertoiresContext", StringComparison.Ordinal) &&
                        !m.DeclaringType.FullName.StartsWith("SolastaUnfinishedBusiness.Models.ThirdCasterProgressionPlan", StringComparison.Ordinal) &&
                        !m.DeclaringType.FullName.StartsWith("SolastaUnfinishedBusiness.Models.PactMagicResourcePlan", StringComparison.Ordinal) &&
                        !m.DeclaringType.FullName.StartsWith("SolastaUnfinishedBusiness.Models.PactMagicMulticasterContext", StringComparison.Ordinal))
            .SelectMany(m => m.Body.Instructions).Where(i => i.Operand is MethodReference r && IsOwn(r.DeclaringType)).ToArray();
        check(externalCalls.Length == 0, "Multicaster compiled metadata isolation: only isolated Stage13.3-13.5 standard/repertoire/Pact components may consume the calculator; existing admission paths remain isolated");
        check(own.All(t => t.CustomAttributes.All(a => !a.AttributeType.Namespace.StartsWith("HarmonyLib", StringComparison.Ordinal))) && root.Fields.Single(f => f.Name == "MixedGameplayReady").Constant is false, "Multicaster compiled metadata isolation: no Harmony patch and readiness gate permanently closed through Stage13.5");
        var dependencies = own.SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Select(i => i.Operand).OfType<MethodReference>().Select(m => m.DeclaringType).ToArray();
        check(dependencies.All(t => IsOwn(t) || t.Namespace.StartsWith("System", StringComparison.Ordinal) || t.Name.StartsWith("FighterProgressionPlan", StringComparison.Ordinal)), "Multicaster compiled metadata isolation: only immutable model/system/shared pure mapping dependencies, no Settings/hero/GetClassLevel/Unity/resource paths");
    }
}
