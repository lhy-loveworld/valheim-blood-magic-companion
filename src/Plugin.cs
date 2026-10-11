using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;

[assembly: AssemblyVersion("1.4.1.0")]
namespace BloodMagicCompanion
{
    [BepInPlugin(Id, "Blood Magic Companion", Version)]
    [BepInIncompatibility("local.valheim.bloodmagiccasterxp")]
    [BepInIncompatibility("local.valheim.bloodmagichealthcost")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "local.valheim.bloodmagiccompanion";
        public const string Version = "1.4.1";
        private Harmony harmony;

        private void Awake()
        {
            // Preserve an explicit 1.0.0 disable when the new setting is first created.
            bool legacyEnabled = Config.Bind("HealthCosts", "Enabled", true,
                "Deprecated: only seeds StaminaCosts/Enabled if the new entry does not exist. Other old HealthCosts settings are ignored.").Value;
            bool stamina = Config.Bind("StaminaCosts", "Enabled", legacyEnabled,
                "Replace Blood Magic eitr with stamina; keep original health costs. Restart required.").Value;
            bool xp = Config.Bind("CasterXP", "Enabled", true,
                "Redirect shield-break XP to the caster when the simulation owner and caster support it. Restart required.").Value;
            bool training = Config.Bind("Summons", "AttackTrainingDummy", false,
                "Allow owned summoned skeletons to attack T.W.I.G. when no ordinary enemy is available. The skeleton simulation owner needs this enabled. Restart required.").Value;
            bool lava = Config.Bind("Summons", "LavaDamageImmunity", false,
                "Protect owned summoned skeletons from lava damage and lava-applied burning. Other fire damage and boiling ocean damage remain. The simulation owner needs this enabled. Restart required.").Value;
            bool trollXp = Config.Bind("TrollXP", "Enabled", false,
                "Award caster Blood Magic XP for summoned troll damage to enemies. Caster and victim simulation owner need this enabled. Resummon trolls after upgrading. Restart required.").Value;
            TrollXP.Configure(Config);
            StaminaCosts.Configure(Config, Logger);
            CasterXP.Configure(Config, Logger);
            harmony = new Harmony(Id);
            try
            {
                if (stamina) StaminaCosts.Patch(harmony);
                if (xp) CasterXP.Patch(harmony);
                if (training) SummonTraining.Patch(harmony);
                if (lava) SummonLavaProtection.Patch(harmony);
                if (trollXp) TrollXP.Patch(harmony);
                // Activate only after every requested patch has installed successfully.
                StaminaCosts.Active = stamina;
                SummonTraining.Active = training;
                SummonLavaProtection.Active = lava;
                CasterXP.SetEnabled(xp);
                TrollXP.SetEnabled(trollXp);
                Logger.LogInfo("Blood Magic Companion " + Version + " loaded. StaminaCosts=" + stamina +
                    "; CasterXP=" + xp + "; AttackTrainingDummy=" + training + "; LavaDamageImmunity=" + lava + "; TrollXP=" + trollXp + ". Server and other clients are optional.");
            }
            catch (Exception error)
            {
                StaminaCosts.Active = false;
                SummonTraining.Active = false;
                SummonLavaProtection.Active = false;
                CasterXP.SetEnabled(false);
                TrollXP.SetEnabled(false);
                harmony.UnpatchSelf();
                Logger.LogError("Could not install all requested patches; vanilla behavior retained. " + error);
            }
        }
        private void Update() { CasterXP.Advertise(Player.m_localPlayer); TrollXP.Advertise(Player.m_localPlayer); }
        private void OnDestroy()
        {
            StaminaCosts.Active = false;
            SummonTraining.Active = false;
            SummonLavaProtection.Active = false;
            CasterXP.SetEnabled(false);
            TrollXP.SetEnabled(false);
            if (harmony != null) harmony.UnpatchSelf();
        }
    }
}
