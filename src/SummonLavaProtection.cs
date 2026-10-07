using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BloodMagicCompanion
{
    internal static class SummonLavaProtection
    {
        internal static bool Active;
        internal static bool Applies(Character character)
        {
            if (!Active || character == null) return false;
            string name = character.gameObject.name;
            return (name == "Skeleton_Friendly" || name == "Skeleton_Friendly(Clone)") &&
                character.IsOwner() && character.IsTamed() && !character.IsDead() &&
                character.GetFaction() == Character.Faction.Players;
        }
        internal static void Patch(Harmony harmony)
        { harmony.CreateClassProcessor(typeof(HeatDamagePatch)).Patch(); }

        [HarmonyPatch(typeof(Character), "UpdateHeatDamage", new Type[] { typeof(float) })]
        internal static class HeatDamagePatch
        {
            // Keep native timing and ocean heat. Jump over both lava damage/burning branches only.
            // Do not mutate heat, fire resistance, hit packets, status effects, or AI state.
            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                List<CodeInstruction> code = new List<CodeInstruction>(instructions);
                FieldInfo lava = AccessTools.Field(typeof(Character), "m_lavaHeatLevel");
                FieldInfo ocean = AccessTools.Field(typeof(Character), "m_ashlandsOceanHeatLevel");
                int first = -1, destination = -1, lavaReads = 0, oceanReads = 0;
                for (int i = 0; i < code.Count; i++)
                {
                    if (code[i].blocks.Count != 0)
                        throw new InvalidOperationException("Unsupported heat-damage exception layout.");
                    if (code[i].opcode != OpCodes.Ldfld) continue;
                    if (Equals(code[i].operand, lava))
                    {
                        lavaReads++;
                        if (first < 0) first = i - 1;
                    }
                    if (Equals(code[i].operand, ocean)) { oceanReads++; destination = i - 1; }
                }
                if (lava == null || ocean == null || lavaReads != 2 || oceanReads != 1 ||
                    first < 0 || destination <= first || code[first].opcode != OpCodes.Ldarg_0 ||
                    code[destination].opcode != OpCodes.Ldarg_0)
                    throw new InvalidOperationException("Expected lava and ocean damage branches were not found.");
                // Reject an interleaved future layout instead of skipping unrelated damage.
                for (int i = destination + 1; i < code.Count; i++)
                    if (code[i].opcode == OpCodes.Ldfld && Equals(code[i].operand, lava))
                        throw new InvalidOperationException("Unexpected lava branch after ocean heat.");
                Label skipLava = generator.DefineLabel();
                code[destination].labels.Add(skipLava);
                CodeInstruction observer = new CodeInstruction(OpCodes.Ldarg_0);
                observer.labels.AddRange(code[first].labels);
                code[first].labels.Clear();
                code.InsertRange(first, new CodeInstruction[] {
                    observer,
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SummonLavaProtection), "Applies")),
                    new CodeInstruction(OpCodes.Brtrue, skipLava) });
                return code;
            }
        }
    }
}
