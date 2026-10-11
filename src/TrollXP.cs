using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
namespace BloodMagicCompanion
{
    internal static class TrollXP
    {
        internal const string CasterKey = Plugin.Id + ".trollCaster.v1";
        internal const string RpcName = "BloodMagicCompanion_TrollHit_v1";
        internal static readonly int ProtocolHash = (Plugin.Id + ".trollXp.v1").GetStableHashCode();
        private static readonly ReceiptLedger receipts = new ReceiptLedger(4096);
        private static ConfigEntry<float> factorSetting;
        private static bool ready;
        internal static void Configure(ConfigFile config)
        {
            factorSetting = config.Bind("TrollXP", "Factor", 0.5f,
                "Blood Magic skill gain factor per accepted damage event (not whole skill levels). Range 0.01-10. Caster setting applies. Restart required.");
        }
        internal static void Patch(Harmony harmony)
        {
            foreach (Type type in new Type[] { typeof(CaptureSpawn), typeof(RewardDamage), typeof(RegisterProtocol) })
                harmony.CreateClassProcessor(type).Patch();
        }
        internal static void SetEnabled(bool enabled)
        { ready = enabled; receipts.Clear(); Advertise(Player.m_localPlayer); }
        internal static void Advertise(Player player)
        {
            if (player == null) return;
            ZNetView view = player.GetComponent<ZNetView>();
            int protocol = ready ? RewardRouting.Protocol : 0;
            if (view != null && view.IsValid() && view.IsOwner() && view.GetZDO().GetInt(ProtocolHash, 0) != protocol)
                view.GetZDO().Set(ProtocolHash, protocol);
        }
        private static ZDO Data(Character character)
        {
            ZNetView view = character == null ? null : character.GetComponent<ZNetView>();
            return view != null && view.IsValid() ? view.GetZDO() : null;
        }
        private static ZDO Lookup(ZDOID id) { return ZDOMan.instance == null ? null : ZDOMan.instance.GetZDO(id); }
        private static Player FindCaster(ZDOID id)
        {
            GameObject obj = ZNetScene.instance == null ? null : ZNetScene.instance.FindInstance(id);
            return obj == null ? null : obj.GetComponent<Player>();
        }
        [HarmonyPatch(typeof(SpawnAbility), "SetupAoe")]
        private static class CaptureSpawn
        {
            // Native argument 'owner' is the new creature; the field is the original caster.
            private static void Prefix(SpawnAbility __instance, Character owner, Character ___m_owner)
            {
                Player caster = ___m_owner as Player;
                if (!ready || caster == null || caster != Player.m_localPlayer || !caster.IsOwner() ||
                    owner == null || !owner.IsOwner() || !TrollXpPolicy.IsSpawn(__instance.gameObject.name) ||
                    !TrollXpPolicy.IsTroll(owner.gameObject.name)) return;
                ZDO data = Data(owner);
                if (data == null) return;
                data.Set(CasterKey, caster.GetZDOID());
                CasterXP.Trace("Tracked Trollstav caster " + caster.GetZDOID() + " for " + owner.GetZDOID());
            }
        }
        internal sealed class DamageCredit
        {
            internal float Before;
            internal ZDOID Caster, Troll, Victim;
        }
        [HarmonyPatch(typeof(Character), "ApplyDamage")]
        private static class RewardDamage
        {
            private static void Prefix(Character __instance, HitData hit, out DamageCredit __state)
            {
                __state = null;
                if (!ready || hit == null || !__instance.IsOwner() || __instance.IsDead()) return;
                Character troll = hit.GetAttacker();
                if (troll == null || !TrollXpPolicy.IsTroll(troll.gameObject.name)) return;
                ZDO data = Data(troll);
                if (data == null) return;
                ZDOID casterId = data.GetZDOID(CasterKey);
                Player caster = FindCaster(casterId);
                if (caster == null || !TrollXpPolicy.Eligible(ready, __instance.IsOwner(), !__instance.IsDead(),
                    __instance.IsPlayer(), __instance.IsTamed(), BaseAI.IsEnemy(caster, __instance),
                    __instance.GetFaction() == Character.Faction.TrainingDummy, troll == __instance, troll.gameObject.name)) return;
                __state = new DamageCredit { Before = __instance.GetHealth(), Caster = casterId,
                    Troll = troll.GetZDOID(), Victim = __instance.GetZDOID() };
            }
            private static void Postfix(Character __instance, DamageCredit __state)
            {
                if (!ready || __state == null || !TrollXpPolicy.LostHealth(__state.Before, __instance.GetHealth())) return;
                Report(__state);
            }
        }
        private static void Report(DamageCredit credit)
        {
            Player local = Player.m_localPlayer;
            bool isLocal = local != null && local.IsOwner() && local.GetZDOID() == credit.Caster;
            ZDO data = Lookup(credit.Caster);
            long peer = data == null ? 0 : data.GetOwner();
            RewardRoute route = RewardRouting.Select(true, false, isLocal,
                data == null ? 0 : data.GetInt(ProtocolHash, 0), peer, ZRoutedRpc.instance != null);
            if (route == RewardRoute.LocalCaster) { Award(local, credit.Troll); return; }
            if (route != RewardRoute.RemoteCaster) return;
            ZPackage packet = new ZPackage();
            packet.Write(credit.Caster); packet.Write(credit.Troll); packet.Write(credit.Victim);
            packet.Write(Guid.NewGuid().ToString("N"));
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, RpcName, packet);
        }
        private static void Award(Player player, ZDOID troll)
        {
            // Never call RaiseSkill on another peer's Player replica.
            if (!ready || player == null || player != Player.m_localPlayer || !player.IsOwner()) return;
            Skills skills = player.GetSkills();
            if (skills == null) return;
            float factor = CostModel.Setting(factorSetting.Value, 0.01f, 10f, 0.5f);
            skills.RaiseSkill(Skills.SkillType.BloodMagic, factor);
            CasterXP.Trace("Awarded Trollstav Blood Magic XP to local caster, factor " + factor + ", troll " + troll);
        }
        private static void Receive(long sender, ZPackage packet)
        {
            if (!ready) return;
            try
            {
                Player local = Player.m_localPlayer;
                if (local == null || !local.IsOwner()) return;
                ZDOID caster = packet.ReadZDOID(), troll = packet.ReadZDOID(), victim = packet.ReadZDOID();
                string token = packet.ReadString();
                if (caster != local.GetZDOID() || victim == caster || victim == troll) return;
                ZDO victimData = Lookup(victim), trollData = Lookup(troll);
                if (trollData == null || trollData.GetZDOID(CasterKey) != caster ||
                    trollData.GetPrefab() != "Troll_Summoned".GetStableHashCode() ||
                    !ReceiptLedger.IsAuthorizedSender(sender, victimData == null ? (long?)null : victimData.GetOwner()) ||
                    !receipts.Accept(sender, token, 1f)) return;
                Award(local, troll);
            }
            catch (Exception error) { CasterXP.Trace("Ignored invalid Trollstav XP report: " + error.Message); }
        }
        [HarmonyPatch(typeof(ZRoutedRpc), MethodType.Constructor, new Type[] { typeof(bool) })]
        private static class RegisterProtocol
        {
            private static void Postfix(ZRoutedRpc __instance)
            { receipts.Clear(); __instance.Register<ZPackage>(RpcName, Receive); }
        }
    }
}
