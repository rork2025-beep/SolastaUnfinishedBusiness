using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SolastaUnfinishedBusiness.Api;
using SolastaUnfinishedBusiness.Api.GameExtensions;

namespace SolastaUnfinishedBusiness.Models;

// Native routines assume one class and read CharacterLevel. Scope corrections to Monk mechanics,
// without replacing CharacterLevel/GetClassLevel or changing an attribute even temporarily.
internal static class MonkNativeProgressionPatches
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static bool slowFallRewritten;
    private static bool deflectMissileRewritten;

    // Harmony 2.0.2 lacks AccessTools.EnumeratorMoveNext and MethodType.Enumerator.
    // Discover the generated MoveNext via its source method and captured action type instead of a compiler index.
    private static MethodBase IteratorTarget(Type actionType)
    {
        var candidates = actionType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Where(x => x.Name.StartsWith("<ExecuteImpl>", StringComparison.Ordinal))
            .Select(x => x.GetMethod("MoveNext", InstanceMembers)).Where(x => x != null).ToArray();
        if (candidates.Length != 1)
        {
            throw new InvalidOperationException("Monk prototype: cannot uniquely locate " + actionType.Name + ".ExecuteImpl iterator.");
        }

        return candidates[0];
    }

    private static FieldInfo ActionField(MethodBase iterator, Type actionType)
    {
        var fields = iterator.DeclaringType.GetFields(InstanceMembers).Where(x => x.FieldType == actionType).ToArray();
        if (fields.Length != 1)
        {
            throw new InvalidOperationException("Monk prototype: cannot uniquely locate captured " + actionType.Name + ".");
        }

        return fields[0];
    }

    private static bool HasPatch(MethodBase original, Type patchType, string method, bool prefix)
    {
        var info = Harmony.GetPatchInfo(original);
        if (info == null)
        {
            return false;
        }

        var patches = prefix ? info.Prefixes : info.Transpilers;
        return patches.Any(x => x.PatchMethod.DeclaringType == patchType && x.PatchMethod.Name == method);
    }

    // Call before enabling Monk compression. A signature/IL mismatch leaves the prototype disabled.
    internal static bool IsReady()
    {
        try
        {
            var movement = DatabaseHelper.GetDefinition<FeatureDefinitionMovementAffinity>("MovementAffinityMonkUnarmoredMovement");
            var movementRanksComplete = Enumerable.Range(2, 19)
                .All(rank => movement.AdditiveModifierByLevelTable.Any(x => x.Rank == rank));
            return movementRanksComplete && slowFallRewritten && deflectMissileRewritten &&
                   HasPatch(IteratorTarget(typeof(CharacterActionSlowFall)), typeof(SlowFallPatch), nameof(SlowFallPatch.Transpiler), false) &&
                   HasPatch(IteratorTarget(typeof(CharacterActionDeflectMissile)), typeof(DeflectMissilePatch), nameof(DeflectMissilePatch.Transpiler), false) &&
                   HasPatch(AccessTools.Method(typeof(RulesetCharacter), nameof(RulesetCharacter.ComputeSpeedAddition)),
                       typeof(MovementPatch), nameof(MovementPatch.Prefix), true);
        }
        catch (Exception e)
        {
            Main.Error("Monk prototype: native patch verification failed: " + e.Message);
            return false;
        }
    }

    private static bool ShouldUse(RulesetCharacter character)
    {
        if (!MonkProgressionContext.Active || character == null)
        {
            return false;
        }

        var actual = character.GetClassLevel(DatabaseHelper.CharacterClassDefinitions.Monk);
        return actual > 0 && actual <= 12;
    }

    internal static int SlowFallLevel(int original, CharacterActionSlowFall action)
    {
        var character = action.ActingCharacter?.RulesetCharacter;
        return ShouldUse(character) && action.ActionDefinition.Id == ActionDefinitions.Id.SlowFall
            ? MonkProgressionContext.EffectiveLevel(character)
            : original;
    }

    internal static int DeflectMissileLevel(int original, CharacterActionDeflectMissile action)
    {
        var character = action.ActingCharacter?.RulesetCharacter;
        return ShouldUse(character) && action.ActionDefinition.Id == ActionDefinitions.Id.DeflectMissileMonk
            ? MonkProgressionContext.EffectiveLevel(character)
            : original;
    }

    [HarmonyPatch]
    internal static class SlowFallPatch
    {
        internal static MethodBase TargetMethod() => IteratorTarget(typeof(CharacterActionSlowFall));

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            slowFallRewritten = false;
            var code = instructions.ToList();
            var matches = Enumerable.Range(0, Math.Max(0, code.Count - 3)).Where(i =>
                code[i].opcode == OpCodes.Ldstr && code[i].operand as string == AttributeDefinitions.CharacterLevel &&
                code[i + 1].opcode == OpCodes.Ldc_I4_0 &&
                code[i + 2].operand is MethodInfo getAttribute && getAttribute.Name == nameof(RulesetEntity.GetAttribute) &&
                code[i + 3].operand is MethodInfo getValue && getValue.Name == "get_CurrentValue").ToArray();
            if (matches.Length != 1)
            {
                Main.Error("Monk prototype: Slow Fall expected one CharacterLevel read; native patch not applied.");
                return code;
            }

            var captured = ActionField(TargetMethod(), typeof(CharacterActionSlowFall));
            code.InsertRange(matches[0] + 4, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, captured),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MonkNativeProgressionPatches), nameof(SlowFallLevel)))
            });
            slowFallRewritten = true;
            return code;
        }
    }

    [HarmonyPatch]
    internal static class DeflectMissilePatch
    {
        internal static MethodBase TargetMethod() => IteratorTarget(typeof(CharacterActionDeflectMissile));

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            deflectMissileRewritten = false;
            var code = instructions.ToList();
            var matches = Enumerable.Range(0, Math.Max(0, code.Count - 1)).Where(i =>
                code[i].opcode == OpCodes.Ldstr && code[i].operand as string == AttributeDefinitions.CharacterLevel &&
                code[i + 1].operand is MethodInfo method && method.Name == nameof(RulesetEntity.TryGetAttributeValue)).ToArray();
            if (matches.Length != 1)
            {
                Main.Error("Monk prototype: Deflect Missile expected one CharacterLevel read; native patch not applied.");
                return code;
            }

            var captured = ActionField(TargetMethod(), typeof(CharacterActionDeflectMissile));
            code.InsertRange(matches[0] + 2, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, captured),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MonkNativeProgressionPatches), nameof(DeflectMissileLevel)))
            });
            deflectMissileRewritten = true;
            return code;
        }
    }

    [HarmonyPatch(typeof(RulesetCharacter), nameof(RulesetCharacter.ComputeSpeedAddition))]
    internal static class MovementPatch
    {
        internal static bool Prefix(RulesetCharacter __instance, IMovementAffinityProvider provider, int addition, ref int __result)
        {
            if (!ShouldUse(__instance) || !ReferenceEquals(provider, DatabaseHelper.GetDefinition<FeatureDefinitionMovementAffinity>("MovementAffinityMonkUnarmoredMovement")))
            {
                return true;
            }

            // Equipment restrictions are checked by the caller; preserve any already accumulated addition.
            var effective = MonkProgressionContext.EffectiveLevel(__instance);
            var rank = provider.AdditiveModifierByLevelTable.FirstOrDefault(x => x.Rank == effective);
            if (rank == null)
            {
                Main.Error("Monk prototype: missing unarmored movement rank " + effective + ".");
                __result = addition + provider.BaseSpeedAdditiveModifier;
                return false;
            }

            __result = addition + rank.Modifier;
            return false;
        }
    }
}
