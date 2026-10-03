using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;


namespace BloodMagicCompanion
{
    internal static class CasterXP
    {
        private const string LegacyId = "local.valheim.bloodmagiccasterxp";
        internal const string RpcName = "BloodMagicCasterXP_Break_v3";
        internal static readonly int ShieldHash = "Staff_shield".GetStableHashCode();
        internal static readonly int ProtocolHash = (LegacyId + ".protocol").GetStableHashCode();
        private static ConditionalWeakTable<SE_Shield, Credit> credits = new ConditionalWeakTable<SE_Shield, Credit>();
        private static readonly ReceiptLedger receipts = new ReceiptLedger(4096);
        private static ManualLogSource log;
        private static ConfigEntry<bool> diagnostics;
        private static bool ready;

        internal static void Configure(ConfigFile config, ManualLogSource logger)
        {
            log = logger;
            diagnostics = config.Bind("Logging", "Diagnostics", false,
                "Log shield attribution and XP delivery for multiplayer troubleshooting.");
        }
        internal static void Patch(Harmony harmony)
        {
            foreach (Type type in new Type[] { typeof(AdvertiseProtocol), typeof(RegisterProtocol),
                typeof(CaptureCaster), typeof(RedirectReward) })
                harmony.CreateClassProcessor(type).Patch();
        }
        internal static void SetEnabled(bool enabled)
        {
            ready = enabled;
            Advertise(Player.m_localPlayer);
        }
        internal static void Advertise(Player player)
        {
            if (player == null) return;
            ZNetView view = player.GetComponent<ZNetView>();
            int protocol = ready ? RewardRouting.Protocol : 0;
            if (view != null && view.IsValid() && view.IsOwner() &&
                view.GetZDO().GetInt(ProtocolHash, 0) != protocol)
                view.GetZDO().Set(ProtocolHash, protocol);
        }

        internal static void Trace(string message)
        {
            if (diagnostics != null && diagnostics.Value) log.LogInfo(message);
        }

        private sealed class Credit
        {
            internal ZDOID Caster;
            internal string Token = Guid.NewGuid().ToString("N");
            internal bool Consumed;
        }

        // Called exactly where vanilla applies the attacker to the accepted status effect.
        // Recasts reach this call too, so the latest caster owns the refreshed shield.
        internal static void RememberCaster(StatusEffect effect, Character attacker)
        {
            effect.SetAttacker(attacker);
            if (!ready) return;
            SE_Shield shield = effect as SE_Shield;
            if (shield == null || shield.NameHash() != ShieldHash) return;
            credits.Remove(shield);
            Player caster = attacker as Player;
            if (caster == null) return;
            ZNetView view = caster.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return;
            // Track identity even before the capability flag has synchronized. Decide
            // compatibility at break time, not when the shield was first cast.
            credits.Add(shield, new Credit { Caster = caster.GetZDOID() });
            Trace("Tracked shield caster " + caster.GetZDOID());
        }

        // Replaces only GetSkills in SE_Shield.IsDone's existing break-reward branch.
        // Null causes vanilla to skip its recipient reward; all visuals and removal run normally.
        internal static Skills GetRewardSkills(Character protectedCharacter, SE_Shield shield)
        {
            if (!ready) return protectedCharacter.GetSkills();
            Credit credit;
            if (shield.NameHash() != ShieldHash || shield.m_levelUpSkillOnBreak != Skills.SkillType.BloodMagic ||
                !credits.TryGetValue(shield, out credit))
                return protectedCharacter.GetSkills();

            Player local = Player.m_localPlayer;
            bool casterIsLocal = local != null && local.IsOwner() && local.GetZDOID() == credit.Caster;
            ZDO casterData = ZDOMan.instance == null ? null : ZDOMan.instance.GetZDO(credit.Caster);
            int protocol = casterData == null ? 0 : casterData.GetInt(ProtocolHash, 0);
            long casterPeer = casterData == null ? 0 : casterData.GetOwner();
            ZRoutedRpc router = ZRoutedRpc.instance;
            RewardRoute route = RewardRouting.Select(protectedCharacter.IsOwner(), credit.Consumed,
                casterIsLocal, protocol, casterPeer, router != null);
            if (route == RewardRoute.Suppress) return null;
            credit.Consumed = true;
            if (route == RewardRoute.Vanilla)
            {
                Trace("Caster unavailable or incompatible; using vanilla shield reward.");
                return protectedCharacter.GetSkills();
            }
            if (route == RewardRoute.LocalCaster)
            {
                // Works for self, summons, and tames without needing a server handler
                // or a Skills component on the protected creature.
                Trace("Shield broke; XP awarded directly to local caster.");
                return local.GetSkills();
            }

            ZPackage packet = new ZPackage();
            packet.Write(credit.Caster);
            packet.Write(protectedCharacter.GetZDOID());
            packet.Write(credit.Token);
            // Preserve the exact factor from this shield, including compatible prefab edits.
            packet.Write(shield.m_levelUpSkillFactor);
            router.InvokeRoutedRPC(casterPeer, RpcName, packet);
            Trace("Redirected shield-break XP to " + credit.Caster + " (recipient receives none).");
            return null;
        }

        internal static void ReceiveBreak(long sender, ZPackage packet)
        {
            if (!ready) return;
            try
            {
                Player local = Player.m_localPlayer;
                if (local == null || !local.IsOwner()) return;
                ZDOID caster = packet.ReadZDOID();
                ZDOID recipient = packet.ReadZDOID();
                string token = packet.ReadString();
                float factor = packet.ReadSingle();
                if (caster != local.GetZDOID() || recipient == caster) return;
                // A creature's creator is often NOT its current simulation owner. Validate
                // the current ZDO owner so another client or the dedicated server can report
                // its shield break. Unknown/stale ownership fails closed, never guesses.
                ZDO recipientData = ZDOMan.instance.GetZDO(recipient);
                if (!ReceiptLedger.IsAuthorizedSender(sender,
                    recipientData == null ? (long?)null : recipientData.GetOwner())) return;
                Skills skills = local.GetSkills();
                if (skills == null || !receipts.Accept(sender, token, factor)) return;
                // Vanilla shield XP calls Skills directly, not Player.RaiseSkill. Preserve that
                // choice so this fix does not add an extra Rested/status-effect multiplier.
                skills.RaiseSkill(Skills.SkillType.BloodMagic, factor);
                Trace("Received shield-break Blood Magic XP, factor " + factor + ".");
            }
            catch (Exception error)
            {
                log.LogWarning("Ignored invalid shield XP message: " + error.Message);
            }
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        private static class AdvertiseProtocol
        {
            private static void Postfix(Player __instance)
            {
                Advertise(__instance);
            }
        }

        [HarmonyPatch(typeof(ZRoutedRpc), MethodType.Constructor, new Type[] { typeof(bool) })]
        private static class RegisterProtocol
        {
            private static void Postfix(ZRoutedRpc __instance)
            {
                credits = new ConditionalWeakTable<SE_Shield, Credit>();
                receipts.Clear();
                __instance.Register<ZPackage>(RpcName, ReceiveBreak);
            }
        }

        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        private static class CaptureCaster
        {
            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return ReplaceCall(instructions, AccessTools.Method(typeof(StatusEffect), "SetAttacker"),
                    AccessTools.Method(typeof(CasterXP), "RememberCaster"), false);
            }
        }

        [HarmonyPatch(typeof(SE_Shield), "IsDone")]
        private static class RedirectReward
        {
            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return ReplaceCall(instructions, AccessTools.Method(typeof(Character), "GetSkills"),
                    AccessTools.Method(typeof(CasterXP), "GetRewardSkills"), true);
            }
        }

        internal static IEnumerable<CodeInstruction> ReplaceCall(IEnumerable<CodeInstruction> instructions,
            MethodInfo original, MethodInfo replacement, bool appendInstance)
        {
            List<CodeInstruction> result = new List<CodeInstruction>();
            int matches = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (!instruction.Calls(original)) { result.Add(instruction); continue; }
                matches++;
                if (appendInstance)
                {
                    CodeInstruction instance = new CodeInstruction(OpCodes.Ldarg_0);
                    instance.labels.AddRange(instruction.labels);
                    instance.blocks.AddRange(instruction.blocks);
                    instruction.labels.Clear();
                    instruction.blocks.Clear();
                    result.Add(instance);
                }
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                result.Add(instruction);
            }
            if (matches != 1) throw new InvalidOperationException("Expected exactly one " + original + "; found " + matches);
            return result;
        }
    }
}
