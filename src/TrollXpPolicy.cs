using System;
using System.Collections.Generic;
namespace BloodMagicCompanion
{
    internal static class TrollXpPolicy
    {
        internal static bool IsTroll(string name) { return name == "Troll_Summoned" || name == "Troll_Summoned(Clone)"; }
        internal static bool IsSpawn(string name) { return name == "staff_redtroll_spawn" || name == "staff_redtroll_spawn(Clone)"; }
        internal static bool Eligible(bool active, bool ownsVictim, bool living, bool player, bool tame,
            bool enemy, bool dummy, bool self, string attacker)
        { return active && ownsVictim && living && !player && !tame && enemy && !dummy && !self && IsTroll(attacker); }
        internal static bool LostHealth(float before, float after)
        { return CostModel.Finite(before) && CostModel.Finite(after) && before > 0 && after < before; }
    }
    // Enforced on the caster, shared by reports from all victim owners. Bounded per session.
    internal sealed class TrollXpCooldown
    {
        private readonly Dictionary<string, double> last = new Dictionary<string, double>();
        private readonly Queue<string> order = new Queue<string>();
        internal void Clear() { last.Clear(); order.Clear(); }
        internal bool Accept(string troll, double now, float interval)
        {
            if (String.IsNullOrEmpty(troll) || Double.IsNaN(now) || Double.IsInfinity(now) || now < 0 ||
                !CostModel.Finite(interval) || interval < 0.25f || interval > 60f) return false;
            double previous;
            if (last.TryGetValue(troll, out previous))
            { if (now - previous < interval) return false; }
            else
            {
                order.Enqueue(troll);
                if (order.Count > 2048) last.Remove(order.Dequeue());
            }
            last[troll] = now;
            return true;
        }
    }
}
