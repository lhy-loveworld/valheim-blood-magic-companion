using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BloodMagicCompanion
{
    internal static class StaminaCosts
    {
        internal static bool Active, Tooltips;
        internal static float EitrRate, SkillReduction;
        private static readonly Dictionary<string, float> Multipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static ConfigFile config;
        private static ManualLogSource log;

        internal static void Configure(ConfigFile settings, ManualLogSource logger)
        {
            config = settings;
            log = logger;
            Multipliers.Clear();
            Tooltips = config.Bind("StaminaCosts", "ShowCostInTooltip", true,
                "Show the additional stamina converted from eitr. Original health costs remain unchanged. Restart required.").Value;
            EitrRate = ReadNumber("StaminaPerEitr", 1f, 0.01f, 100f,
                "Stamina per BASE eitr point, before the Blood Magic skill discount. This is a fixed amount, not a fraction of remaining stamina.");
            SkillReduction = ReadNumber("CostReductionAtSkill100", 0.33f, 0f, 0.9f,
                "Discount on converted stamina at Blood Magic level 100. Applied once; 0.33 means 33%. Does not change vanilla health or native stamina costs.");
            string overrides = config.Bind("StaminaCosts", "ItemCostMultipliers", "StaffShield=1;StaffSkeleton=1;StaffTroll=1",
                "Prefab-name=multiplier entries separated by semicolons. Applies only to converted stamina. Unknown items use 1; range 0.01 to 100; last valid duplicate wins. Restart required.").Value;
            foreach (string entry in overrides.Split(';'))
            {
                if (String.IsNullOrWhiteSpace(entry)) continue;
                string[] pair = entry.Split('='); float value;
                if (pair.Length != 2 || String.IsNullOrWhiteSpace(pair[0]) ||
                    !float.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                    !CostModel.Finite(value) || value < 0.01f || value > 100f)
                { log.LogWarning("Ignoring invalid ItemCostMultipliers entry: " + entry); continue; }
                Multipliers[pair[0].Trim()] = value;
            }
        }
        internal static void Patch(Harmony harmony)
        {
            foreach (Type type in new Type[] { typeof(EitrCostPatch), typeof(TooltipEitrPatch),
                typeof(StaminaCostPatch), typeof(CostTooltipPatch) })
                harmony.CreateClassProcessor(type).Patch();
        }
        private static float ReadNumber(string key, float fallback, float minimum, float maximum, string description)
        {
            float configured = config.Bind("StaminaCosts", key, fallback,
                description + " Valid range: " + minimum.ToString(CultureInfo.InvariantCulture) + " to " + maximum.ToString(CultureInfo.InvariantCulture) + ". Restart required.").Value;
            float result = CostModel.Setting(configured, minimum, maximum, fallback);
            if (result != configured) log.LogWarning("Invalid " + key + "; using " + fallback.ToString(CultureInfo.InvariantCulture));
            return result;
        }
        internal static bool Applies(Attack attack, Character character, ItemDrop.ItemData weapon)
        {
            return Active && attack != null && character != null && character == Player.m_localPlayer &&
                character.IsOwner() && weapon != null && weapon.m_shared != null &&
                weapon.m_shared.m_skillType == Skills.SkillType.BloodMagic &&
                attack.m_attackEitr > 0 && !attack.m_attackKillsSelf;
        }
        internal static float Surcharge(Attack attack, Character character, ItemDrop.ItemData weapon)
        {
            float multiplier = 1f;
            if (weapon.m_dropPrefab != null) Multipliers.TryGetValue(weapon.m_dropPrefab.name, out multiplier);
            if (multiplier <= 0) multiplier = 1f;
            return CostModel.Surcharge(attack.m_attackEitr,
                character.GetSkillFactor(Skills.SkillType.BloodMagic), EitrRate, SkillReduction, multiplier);
        }
    }

    [HarmonyPatch(typeof(Attack), "GetAttackEitr", new Type[] { })]
    internal static class EitrCostPatch
    {
        private static bool Prefix(Attack __instance, Humanoid ___m_character, ItemDrop.ItemData ___m_weapon, ref float __result)
        {
            if (!StaminaCosts.Applies(__instance, ___m_character, ___m_weapon)) return true;
            __result = 0f; return false;
        }
    }
    [HarmonyPatch(typeof(Attack), "GetAttackEitr", new Type[] { typeof(Character), typeof(ItemDrop.ItemData) })]
    internal static class TooltipEitrPatch
    {
        private static bool Prefix(Attack __instance, Character __0, ItemDrop.ItemData __1, ref float __result)
        {
            if (!StaminaCosts.Applies(__instance, __0, __1)) return true;
            __result = 0f; return false;
        }
    }
    [HarmonyPatch(typeof(Attack), "GetAttackStamina", new Type[] { })]
    internal static class StaminaCostPatch
    {
        // Add after vanilla modifiers; never change shared Attack fields or manually deduct resources.
        private static void Postfix(Attack __instance, Humanoid ___m_character, ItemDrop.ItemData ___m_weapon, ref float __result)
        {
            if (!StaminaCosts.Applies(__instance, ___m_character, ___m_weapon)) return;
            __result = CostModel.Total(__result, StaminaCosts.Surcharge(__instance, ___m_character, ___m_weapon));
        }
    }
    [HarmonyPatch(typeof(ItemDrop.ItemData), "GetTooltip", new Type[] {
        typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
    internal static class CostTooltipPatch
    {
        private static void Postfix(ItemDrop.ItemData __0, ref string __result)
        {
            if (!StaminaCosts.Active || !StaminaCosts.Tooltips || __0 == null || __0.m_shared == null || Player.m_localPlayer == null) return;
            string primary = Line("Primary", __0.m_shared.m_attack, __0);
            string secondary = Line("Secondary", __0.m_shared.m_secondaryAttack, __0);
            if (primary.Length + secondary.Length == 0) return;
            __result += "\n\n<color=orange>Stamina-powered Blood Magic</color>" + primary + secondary +
                "\nAdded to any native stamina cost. Original health cost unchanged. Enough stamina is required.";
        }
        private static string Line(string label, Attack attack, ItemDrop.ItemData item)
        {
            if (!StaminaCosts.Applies(attack, Player.m_localPlayer, item)) return "";
            float cost = StaminaCosts.Surcharge(attack, Player.m_localPlayer, item);
            if (cost == float.MaxValue) return "\n" + label + ": unavailable (invalid or excessive cost)";
            return "\n" + label + ": <color=orange>+" + cost.ToString("0.00", CultureInfo.InvariantCulture) +
                " stamina</color> from eitr, 0 eitr. Includes your Blood Magic skill.";
        }
    }
}
