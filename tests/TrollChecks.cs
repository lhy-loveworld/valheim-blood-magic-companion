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
        TrollXpCooldown gate=new TrollXpCooldown();
        Check(gate.Accept("trollA",10,1), "first hit");
        Check(!gate.Accept("trollA",10,1), "area hit cannot multiply reward");
        Check(!gate.Accept("trollA",10.9,1), "other victim owner uses same cooldown");
        Check(gate.Accept("trollB",10,1), "second troll independent");
        Check(gate.Accept("trollA",11,1), "next eligible hit");
        Check(!gate.Accept("trollA",5,1), "clock rollback rejected");
        Check(!gate.Accept("trollC",double.NaN,1), "invalid clock");
        Check(!gate.Accept("trollC",12,0), "unsafe interval");
        Check(!gate.Accept("trollC",12,float.NaN), "NaN interval");
        Check(!gate.Accept("",12,1), "missing identity");
        gate.Clear(); Check(gate.Accept("trollA",0,1), "new session clears clock and identity");
        ReceiptLedger ledger=new ReceiptLedger(4); string token=Guid.NewGuid().ToString("N");
        Check(ledger.Accept(8,token,1), "remote event first receipt");
        Check(!ledger.Accept(8,token,1), "duplicate remote event");
        Check(!ReceiptLedger.IsAuthorizedSender(8,9), "old victim owner rejected");
        Check(ReceiptLedger.IsAuthorizedSender(9,9), "current victim owner accepted");
        Check(RewardRouting.Select(true,false,false,0,9,true)==RewardRoute.Vanilla,"unmodded caster receives no custom event");
        Check(RewardRouting.Select(true,false,true,0,0,false)==RewardRoute.LocalCaster,"local caster needs no network router");
        Check(RewardRouting.Select(true,false,false,3,9,true)==RewardRoute.RemoteCaster,"capable caster receives targeted event");
        Console.WriteLine("PASS: " + count + " Trollstav eligibility, health-loss, cooldown and routing assertions (not Unity).");
    }
    private static bool Eligible(bool[] b) { return TrollXpPolicy.Eligible(b[0],b[1],b[2],b[3],b[4],b[5],b[6],b[7],"Troll_Summoned(Clone)"); }
}
