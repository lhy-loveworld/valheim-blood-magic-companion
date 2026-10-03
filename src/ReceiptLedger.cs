using System;
using System.Collections.Generic;

namespace BloodMagicCompanion
{
    // Bounded duplicate protection, shared by production code and the executable tests.
    internal sealed class ReceiptLedger
    {
        private readonly int capacity;
        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly Queue<string> order = new Queue<string>();
        internal ReceiptLedger(int capacity) { this.capacity = capacity; }
        internal void Clear() { seen.Clear(); order.Clear(); }
        internal static bool IsAuthorizedSender(long sender, long? currentOwner)
        {
            return sender != 0 && currentOwner.HasValue && currentOwner.Value == sender;
        }
        internal bool Accept(long sender, string token, float factor)
        {
            Guid parsed;
            if (sender == 0 || token == null || token.Length != 32 || !Guid.TryParseExact(token, "N", out parsed) ||
                float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0 || factor > 100) return false;
            string key = sender.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + token;
            if (!seen.Add(key)) return false;
            order.Enqueue(key);
            while (order.Count > capacity) seen.Remove(order.Dequeue());
            return true;
        }
    }
}

namespace BloodMagicCompanion
{
    internal enum RewardRoute { Vanilla, Suppress, LocalCaster, RemoteCaster }

    // Server presence is deliberately not an input: vanilla routing forwards targeted
    // messages without a server plugin. Unknown/mismatched clients are never targeted.
    internal static class RewardRouting
    {
        internal const int Protocol = 3;
        internal static RewardRoute Select(bool ownsRecipient, bool consumed,
            bool casterIsLocal, int casterProtocol, long casterPeer, bool routerAvailable)
        {
            if (!ownsRecipient || consumed) return RewardRoute.Suppress;
            if (casterIsLocal) return RewardRoute.LocalCaster;
            if (casterProtocol == Protocol && casterPeer != 0 && routerAvailable)
                return RewardRoute.RemoteCaster;
            return RewardRoute.Vanilla;
        }
    }
}
