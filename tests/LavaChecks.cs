using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BloodMagicCompanion;
using HarmonyLib;
using UnityEngine;
internal static class LavaChecks
{
    private static int count, lavaHits, oceanHits, burns, ticks;
    private static void Check(bool value, string label)
    { if (!value) throw new Exception("FAILED lava: " + label); count++; }
    public static void LavaHit() { lavaHits++; burns++; }
    public static void OceanHit() { oceanHits++; }
    public static void Tick() { ticks++; }
    private static CodeInstruction Op(OpCode code, object value=null) { return new CodeInstruction(code,value); }
    private static List<CodeInstruction> NativeFixture(ILGenerator il)
    {
        Label partial=il.DefineLabel(), ocean=il.DefineLabel(), done=il.DefineLabel();
        FieldInfo lava=AccessTools.Field(typeof(Character),"m_lavaHeatLevel");
        FieldInfo sea=AccessTools.Field(typeof(Character),"m_ashlandsOceanHeatLevel");
        List<CodeInstruction> code=new List<CodeInstruction> {
            Op(OpCodes.Call,AccessTools.Method(typeof(LavaChecks),"Tick")),
            Op(OpCodes.Ldarg_0),Op(OpCodes.Ldfld,lava),Op(OpCodes.Ldc_R4,1f),Op(OpCodes.Blt_Un,partial),
            Op(OpCodes.Call,AccessTools.Method(typeof(LavaChecks),"LavaHit")),Op(OpCodes.Ret),
            Op(OpCodes.Ldarg_0),Op(OpCodes.Ldfld,lava),Op(OpCodes.Ldc_R4,0.7f),Op(OpCodes.Blt_Un,ocean),
            Op(OpCodes.Call,AccessTools.Method(typeof(LavaChecks),"LavaHit")),Op(OpCodes.Ret),
            Op(OpCodes.Ldarg_0),Op(OpCodes.Ldfld,sea),Op(OpCodes.Ldc_R4,0.7f),Op(OpCodes.Blt_Un,done),
            Op(OpCodes.Call,AccessTools.Method(typeof(LavaChecks),"OceanHit")),Op(OpCodes.Ret) };
        code[7].labels.Add(partial); code[13].labels.Add(ocean); code[18].labels.Add(done);
        return code;
    }
    private static Action<Character> Compile(bool patched)
    {
        DynamicMethod method=new DynamicMethod("HeatDamageProbe",typeof(void),new Type[]{typeof(Character)},typeof(LavaChecks),true);
        ILGenerator il=method.GetILGenerator();
        List<CodeInstruction> code=NativeFixture(il);
        if(patched) code=new List<CodeInstruction>(SummonLavaProtection.HeatDamagePatch.Transpiler(code,il));
        foreach(CodeInstruction i in code)
        {
            foreach(Label label in i.labels) il.MarkLabel(label);
            if(i.operand==null) il.Emit(i.opcode);
            else if(i.operand is MethodInfo) il.Emit(i.opcode,(MethodInfo)i.operand);
            else if(i.operand is FieldInfo) il.Emit(i.opcode,(FieldInfo)i.operand);
            else if(i.operand is Label) il.Emit(i.opcode,(Label)i.operand);
            else il.Emit(i.opcode,(float)i.operand);
        }
        return (Action<Character>)method.CreateDelegate(typeof(Action<Character>));
    }
    private static void RunCase(Action<Character> update, Character c, float lava, float ocean, int hits, int seaHits, string label)
    {
        lavaHits=oceanHits=burns=ticks=0;
        c.m_lavaHeatLevel=lava; c.m_ashlandsOceanHeatLevel=ocean;
        update(c);
        Check(lavaHits==hits && oceanHits==seaHits && burns==hits && ticks==1,label);
        Check(c.m_lavaHeatLevel==lava && c.m_ashlandsOceanHeatLevel==ocean,label+" preserves heat state");
    }
    private static void Reject(List<CodeInstruction> code, ILGenerator il, string label)
    {
        bool rejected=false;
        try { SummonLavaProtection.HeatDamagePatch.Transpiler(code,il); }
        catch(InvalidOperationException) { rejected=true; }
        Check(rejected,label);
    }
    internal static void Run()
    {
        Character c=new Character(); new GameObject(SummonPrefabFacts.SkeletonName+"(Clone)").Add(c);
        c.Team=(Character.Faction)SummonPrefabFacts.SkeletonFaction;
        Action<Character> native=Compile(false), patched=Compile(true);
        SummonLavaProtection.Active=true;
        RunCase(native,c,1f,0,1,0,"control fixture receives full lava damage");
        RunCase(native,c,0.8f,0,1,0,"control fixture receives partial lava damage");
        SummonTraining.Active=false;
        RunCase(patched,c,1f,0,0,0,"lava immunity works with training disabled");
        RunCase(patched,c,0.8f,0,0,0,"initial lava damage and ignition suppressed");
        RunCase(patched,c,0.3f,0,0,0,"below-threshold exposure unaffected");
        RunCase(patched,c,0,1f,0,1,"boiling ocean damage remains");
        RunCase(patched,c,1f,1f,0,1,"ocean branch still runs with lava immunity");
        SummonLavaProtection.Active=false; SummonTraining.Active=true;
        RunCase(patched,c,1f,0,1,0,"disabled lava option remains independent of training");
        SummonLavaProtection.Active=true; c.Owner=false;
        RunCase(patched,c,1f,0,1,0,"unowned skeleton retains native damage");
        c.Owner=true; c.Tamed=false;
        RunCase(patched,c,1f,0,1,0,"untamed creature excluded");
        c.Tamed=true; c.Dead=true;
        Check(!SummonLavaProtection.Applies(c),"dead skeleton excluded");
        c.Dead=false; c.Team=Character.Faction.Undead;
        Check(!SummonLavaProtection.Applies(c),"wrong faction excluded");
        c.Team=Character.Faction.Players; c.gameObject.name="Player(Clone)";
        RunCase(patched,c,1f,0,1,0,"player remains vulnerable");
        c.gameObject.name="Wolf(Clone)";
        RunCase(patched,c,1f,0,1,0,"other tames remain vulnerable");
        c.gameObject.name="Skeleton(Clone)";
        Check(!SummonLavaProtection.Applies(c),"wild skeleton prefab excluded");
        c.gameObject.name="Skeleton_Friendly_Custom(Clone)";
        Check(!SummonLavaProtection.Applies(c),"similar prefab names excluded");
        c.gameObject.name=SummonPrefabFacts.SkeletonName;
        Check(SummonLavaProtection.Applies(c),"exact prefab accepted with actual faction");
        Check(!SummonLavaProtection.Applies(null),"null character excluded");
        DynamicMethod probe=new DynamicMethod("LayoutProbe",typeof(void),new Type[]{typeof(Character)},typeof(LavaChecks),true);
        ILGenerator il=probe.GetILGenerator();
        Reject(new List<CodeInstruction>(),il,"missing layout rejected");
        List<CodeInstruction> code=NativeFixture(il);
        code.Insert(2,Op(OpCodes.Ldfld,AccessTools.Field(typeof(Character),"m_lavaHeatLevel")));
        Reject(code,il,"extra lava branch rejected");
        code=NativeFixture(il); code[1].opcode=OpCodes.Ldarg_1;
        Reject(code,il,"unexpected observer load rejected");
        code=NativeFixture(il); code[0].blocks.Add(new object());
        Reject(code,il,"exception layout rejected");
        code=NativeFixture(il); code.Add(Op(OpCodes.Ldfld,AccessTools.Field(typeof(Character),"m_ashlandsOceanHeatLevel")));
        Reject(code,il,"ambiguous ocean branch rejected");
        code=NativeFixture(il); Label entry=il.DefineLabel(); code[1].labels.Add(entry);
        List<CodeInstruction> result=new List<CodeInstruction>(SummonLavaProtection.HeatDamagePatch.Transpiler(code,il));
        Check(result[1].labels.Contains(entry) && result[4].labels.Count==0,"incoming labels preserved on inserted guard");
        SummonLavaProtection.Active=false; SummonTraining.Active=false;
        Console.WriteLine("PASS: "+count+" lava eligibility and emitted-branch assertions (simulated APIs, not Unity).");
    }
}
