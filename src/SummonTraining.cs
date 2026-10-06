using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace BloodMagicCompanion
{
    internal static class SummonTraining
    {
        internal static bool Active;
        internal static readonly MethodInfo RaycastMethod = AccessTools.Method(typeof(Physics), "Raycast",
            new Type[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(int) });

        internal static void Patch(Harmony harmony)
        {
            harmony.CreateClassProcessor(typeof(FindEnemyPatch)).Patch();
            harmony.CreateClassProcessor(typeof(VisibilityPatch)).Patch();
        }

        private static bool Prefab(Character character, string name)
        {
            return character != null && (character.gameObject.name == name ||
                character.gameObject.name == name + "(Clone)");
        }
        internal static bool Eligible(Character character)
        {
            return Active && Prefab(character, "Skeleton_Friendly") && character.IsOwner() &&
                character.IsTamed() && !character.IsDead() &&
                // Skeleton_Friendly belongs to Players (0), not PlayerSpawned (11).
                character.GetFaction() == Character.Faction.Players;
        }
        internal static bool IsDummy(Character character)
        {
            return Prefab(character, "piece_TrainingDummy") &&
                character.GetFaction() == Character.Faction.TrainingDummy;
        }

        // Called only after vanilla has had its chance to find an ordinary enemy.
        internal static Character SelectTarget(BaseAI ai, Character character, Character original)
        {
            if (original != null || ai == null || !Eligible(character)) return original;
            Character current = ai.GetTargetCreature();
            if (current != null && !current.IsDead() && !IsDummy(current)) return original;
            Character nearest = null;
            float distance = float.PositiveInfinity;
            foreach (Character candidate in Character.GetAllCharacters())
            {
                if (!IsDummy(candidate) || candidate.IsDead()) continue;
                float candidateDistance = Vector3.Distance(ai.transform.position, candidate.transform.position);
                if (candidateDistance >= distance || !ai.CanSeeTarget(candidate)) continue;
                nearest = candidate;
                distance = candidateDistance;
            }
            return nearest;
        }

        // Keep the native ray's origin, direction, length, layers, and trigger handling.
        // Only the selected dummy's own colliders cease to be sight blockers.
        internal static bool SightBlocked(Vector3 origin, Vector3 direction, float distance, int mask,
            Transform self, Character target)
        {
            if (!Active || self == null || !IsDummy(target) || !Eligible(self.GetComponent<Character>()))
                return Physics.Raycast(origin, direction, distance, mask);
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, mask))
                if (hit.collider == null || hit.collider.GetComponentInParent<Character>() != target)
                    return true;
            return false;
        }

        [HarmonyPatch(typeof(BaseAI), "FindEnemy", new Type[] { })]
        internal static class FindEnemyPatch
        {
            private static void Postfix(BaseAI __instance, Character ___m_character, ref Character __result)
            { __result = SelectTarget(__instance, ___m_character, __result); }
        }

        [HarmonyPatch(typeof(BaseAI), "CanSeeTarget", new Type[] { typeof(Transform), typeof(Vector3),
            typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(Character) })]
        internal static class VisibilityPatch
        {
            // Replace only the obstruction test. Vanilla range, view cone, stealth and mist checks stay intact.
            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> code = new List<CodeInstruction>(instructions);
                int found = -1;
                for (int i = 0; i < code.Count; i++)
                {
                    if (code[i].opcode != OpCodes.Call || !Equals(code[i].operand, RaycastMethod)) continue;
                    if (found >= 0 || code[i].blocks.Count != 0)
                        throw new InvalidOperationException("Unsupported BaseAI visibility raycast layout.");
                    found = i;
                }
                if (RaycastMethod == null || found < 0)
                    throw new InvalidOperationException("Expected BaseAI visibility raycast was not found.");
                CodeInstruction self = new CodeInstruction(OpCodes.Ldarg_0);
                self.labels.AddRange(code[found].labels);
                code[found].labels.Clear();
                code[found].operand = AccessTools.Method(typeof(SummonTraining), "SightBlocked");
                code.Insert(found, self);
                code.Insert(found + 1, new CodeInstruction(OpCodes.Ldarg_S, (byte)6));
                return code;
            }
        }
    }
}
