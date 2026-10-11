using System;
using BloodMagicCompanion;
internal static class TrollChecks
{
    private static int count;
    private static void Check(bool value, string label) { if (!value) throw new Exception("FAILED TrollXP: " + label); count++; }
    internal static void Run()
    {
        Check(TrollXpPolicy.IsTroll("Troll_Summoned(Clone)"), "summoned troll clone");
        Check(TrollXpPolicy.IsTroll("Troll_Summoned"), "summoned troll prefab");
        Check(!TrollXpPolicy.IsTroll("Troll(Clone)"), "wild troll excluded");
        Check(!TrollXpPolicy.IsTroll("Troll_Summoned_Custom(Clone)"), "other mod prefab excluded");
        Check(!TrollXpPolicy.IsTroll(null), "null excluded");
        Check(TrollXpPolicy.IsSpawn("staff_redtroll_spawn(Clone)"), "real spawn prefab");
        Check(!TrollXpPolicy.IsSpawn("Spawner_Troll(Clone)"), "world spawner excluded");
        bool[] valid = {true,true,true,false,false,true,false,false};
        Check(Eligible(valid), "owned living enemy eligible");
        for(int i=0;i<valid.Length;i++) { bool[] altered=(bool[])valid.Clone(); altered[i]=!altered[i]; Check(!Eligible(altered), "eligibility gate " + i); }
        Check(TrollXpPolicy.LostHealth(100,90), "damaging hit");
        Check(TrollXpPolicy.LostHealth(100,-5), "lethal hit");
        Check(!TrollXpPolicy.LostHealth(100,100), "immune or blocked hit");
        Check(!TrollXpPolicy.LostHealth(0,-5), "corpse hit");
        Check(!TrollXpPolicy.LostHealth(100,110), "healing");
        Check(!TrollXpPolicy.LostHealth(float.NaN,0), "invalid prior health");
        Check(!TrollXpPolicy.LostHealth(100,float.NegativeInfinity), "invalid resulting health");
        ReceiptLedger ledger=new ReceiptLedger(4); string token=Guid.NewGuid().ToString("N");
        Check(ledger.Accept(8,token,1), "remote event first receipt");
        Check(!ledger.Accept(8,token,1), "duplicate remote event");
        // Distinct damage events must all survive receipt validation, with no elapsed-time requirement.
        Check(ledger.Accept(8,Guid.NewGuid().ToString("N"),1), "second enemy in same area attack rewarded");
        Check(ledger.Accept(8,Guid.NewGuid().ToString("N"),1), "rapid follow-up damage rewarded");
        Check(ledger.Accept(9,Guid.NewGuid().ToString("N"),1), "simultaneous event from another victim owner rewarded");
        Check(!ledger.Accept(8,token,1), "original event remains deduplicated after other hits");
        Check(!ReceiptLedger.IsAuthorizedSender(8,9), "old victim owner rejected");
        Check(ReceiptLedger.IsAuthorizedSender(9,9), "current victim owner accepted");
        Check(RewardRouting.Select(true,false,false,0,9,true)==RewardRoute.Vanilla,"unmodded caster receives no custom event");
        Check(RewardRouting.Select(true,false,true,0,0,false)==RewardRoute.LocalCaster,"local caster needs no network router");
        Check(RewardRouting.Select(true,false,false,3,9,true)==RewardRoute.RemoteCaster,"capable caster receives targeted event");
        Console.WriteLine("PASS: " + count + " Trollstav eligibility, health-loss, multi-hit receipt and routing assertions (not Unity).");
    }
    private static bool Eligible(bool[] b) { return TrollXpPolicy.Eligible(b[0],b[1],b[2],b[3],b[4],b[5],b[6],b[7],"Troll_Summoned(Clone)"); }
}
