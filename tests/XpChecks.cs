using System;
namespace BloodMagicCompanion
{
    internal static class XpChecks
    {
        private static int count;
        private static void Check(bool condition, string label)
        {
            if (!condition) throw new Exception("FAIL: " + label);
            count++;
            Console.WriteLine("PASS: " + label);
        }
        public static int Run()
        {
            ReceiptLedger ledger = new ReceiptLedger(3);
            string a = Guid.NewGuid().ToString("N"), b = Guid.NewGuid().ToString("N");
            Check(ledger.Accept(1, a, 1f), "first valid reward accepted");
            Check(!ledger.Accept(1, a, 1f), "duplicate reward rejected");
            Check(ledger.Accept(2, a, 1f), "different sender tracked independently");
            Check(!ledger.Accept(0, b, 1f), "invalid sender rejected");
            Check(!ledger.Accept(1, "bad", 1f), "invalid token rejected");
            Check(!ledger.Accept(1, b, float.NaN), "NaN rejected");
            Check(!ledger.Accept(1, b, float.PositiveInfinity), "infinity rejected");
            Check(!ledger.Accept(1, b, -1), "negative factor rejected");
            Check(!ledger.Accept(1, b, 0), "zero factor rejected");
            Check(!ledger.Accept(1, b, 101), "out-of-range factor rejected");
            Check(ledger.Accept(1, b, 0.5f), "invalid messages do not consume valid token");
            ledger.Clear();
            Check(ledger.Accept(1, a, 1f), "new network session clears deduplication state");
            Check(ledger.Accept(1, b, 1f), "distinct shields each get credit");
            Check(ledger.Accept(1, Guid.NewGuid().ToString("N"), 1f), "third shield accepted");
            Check(ledger.Accept(1, Guid.NewGuid().ToString("N"), 1f), "ledger remains usable at capacity");
            Check(!ledger.Accept(1, b, 1f), "recent duplicate still rejected after eviction");
            Check(ReceiptLedger.IsAuthorizedSender(20, 20), "current creature owner can report a break");
            Check(!ReceiptLedger.IsAuthorizedSender(10, 20), "original creator cannot report after ownership transfer");
            Check(ReceiptLedger.IsAuthorizedSender(30, 30), "dedicated server can report creatures it owns");
            Check(!ReceiptLedger.IsAuthorizedSender(30, null), "unknown ownership does not authorize a reward");
            Check(!ReceiptLedger.IsAuthorizedSender(0, 0), "zero owner and sender rejected");
            Check(RewardRouting.Select(true, false, true, 0, 0, false) == RewardRoute.LocalCaster,
                "local caster works without capability sync or network router");
            Check(RewardRouting.Select(true, false, false, 3, 20, true) == RewardRoute.RemoteCaster,
                "compatible caster receives targeted XP with no server capability requirement");
            Check(RewardRouting.Select(true, false, false, 0, 20, true) == RewardRoute.Vanilla,
                "unmodded caster keeps vanilla reward and receives no custom message");
            Check(RewardRouting.Select(true, false, false, 2, 20, true) == RewardRoute.Vanilla,
                "older mod version can coexist using vanilla fallback");
            Check(RewardRouting.Select(true, false, false, 4, 20, true) == RewardRoute.Vanilla,
                "unknown newer protocol safely falls back");
            Check(RewardRouting.Select(true, false, false, 3, 0, true) == RewardRoute.Vanilla,
                "zero peer never broadcasts XP to unmodded players");
            Check(RewardRouting.Select(true, false, false, 3, 20, false) == RewardRoute.Vanilla,
                "missing router preserves vanilla reward");
            Check(RewardRouting.Select(true, false, false, 0, 0, true) == RewardRoute.Vanilla,
                "missing caster network data preserves vanilla reward");
            Check(RewardRouting.Select(false, false, true, 3, 20, true) == RewardRoute.Suppress,
                "non-owner observer cannot double-credit a local caster");
            Check(RewardRouting.Select(false, false, false, 3, 20, true) == RewardRoute.Suppress,
                "non-owner observer cannot send XP");
            Check(RewardRouting.Select(true, true, true, 3, 20, true) == RewardRoute.Suppress,
                "repeated local break cannot award XP twice");
            Check(RewardRouting.Select(true, true, false, 3, 20, true) == RewardRoute.Suppress,
                "repeated remote break cannot send XP twice");
            Check(RewardRouting.Select(true, false, false, 3, -20, true) == RewardRoute.RemoteCaster,
                "valid negative-valued peer IDs remain supported");
            Console.WriteLine(count + " receipt, ownership, and mixed-client routing checks passed.");
            return 0;
        }
    }
}
