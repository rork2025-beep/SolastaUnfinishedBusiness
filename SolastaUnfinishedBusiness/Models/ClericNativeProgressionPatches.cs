using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;

namespace SolastaUnfinishedBusiness.Models;

// Scope native CharacterLevel success calculation to the installed Cleric intervention powers only.
internal static class ClericNativeProgressionPatches
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static bool interventionRewritten;

    private static MethodBase IteratorTarget()
    {
        var methods = typeof(CharacterActionUsePower).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Where(x => x.Name.StartsWith("<CheckExecutionFailure>", StringComparison.Ordinal))
            .Select(x => x.GetMethod("MoveNext", Members)).Where(x => x != null).ToArray();
        if (methods.Length != 1) throw new InvalidOperationException("Cleric prototype: cannot locate the intervention failure iterator.");
        return methods[0];
    }
    internal static bool IsReady()
    {
        try
        {
            var info = Harmony.GetPatchInfo(IteratorTarget());
            return interventionRewritten && info != null && info.Transpilers.Any(x =>
                x.PatchMethod.DeclaringType == typeof(InterventionPatch) && x.PatchMethod.Name == nameof(InterventionPatch.Transpiler));
        }
        catch (Exception ex)
        { Main.Error("Cleric native adapter verification failed: " + ex.Message); return false; }
    }
    internal static int InterventionLevel(int original, CharacterActionUsePower action)
    {
        var character = action.ActingCharacter?.RulesetCharacter;
        var power = action.activePower?.PowerDefinition;
        if (!ClericProgressionContext.Active || character == null ||
            character.GetClassLevel(DatabaseHelper.CharacterClassDefinitions.Cleric) is < 1 or > 12) return original;
        return ReferenceEquals(power, DatabaseHelper.FeatureDefinitionPowers.PowerClericDivineInterventionCleric) ||
               ReferenceEquals(power, DatabaseHelper.FeatureDefinitionPowers.PowerClericDivineInterventionPaladin) ||
               ReferenceEquals(power, DatabaseHelper.FeatureDefinitionPowers.PowerClericDivineInterventionWizard)
            ? ClericProgressionContext.EffectiveLevel(character) : original;
    }

    [HarmonyPatch]
    internal static class InterventionPatch
    {
        internal static MethodBase TargetMethod() => IteratorTarget();
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            interventionRewritten = false;
            var code = instructions.ToList();
            var matches = Enumerable.Range(0, Math.Max(0, code.Count - 1)).Where(i =>
                code[i].opcode == OpCodes.Ldstr && code[i].operand as string == AttributeDefinitions.CharacterLevel &&
                code[i + 1].operand is MethodInfo method && method.Name == nameof(RulesetEntity.TryGetAttributeValue)).ToArray();
            var fields = TargetMethod().DeclaringType.GetFields(Members).Where(x => x.FieldType == typeof(CharacterActionUsePower)).ToArray();
            if (matches.Length != 1 || fields.Length != 1)
            {
                Main.Error("Cleric prototype: expected one native intervention level read/captured action; adapter not applied.");
                return code;
            }
            // Preserve the native RNG, comparison, notifications, execution failure and recharge paths.
            code.InsertRange(matches[0] + 2, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, fields[0]),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ClericNativeProgressionPatches), nameof(InterventionLevel)))
            });
            interventionRewritten = true;
            return code;
        }
    }
}
