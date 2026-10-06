using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BloodMagicCompanion;
using HarmonyLib;
using UnityEngine;
internal static class SummonChecks
{
    private static int count;
    private static void Check(bool value,string label)
    { if(!value) throw new Exception("FAILED summons: "+label); count++; }
    private static Character Make(string name, Character.Faction faction, float x)
    {
        Character c=new Character(); new GameObject(name).Add(c);
        c.Team=faction; c.transform.position=new Vector3(x,0,0); return c;
    }
    private static RaycastHit Hit(Character owner)
    { return new RaycastHit { collider=new Collider { ParentCharacter=owner } }; }
    private static bool Blocked(Character s,Character d)
    { return SummonTraining.SightBlocked(new Vector3(1,2,3),new Vector3(4,5,6),17,1024,s.transform,d); }
    private static void Throws(List<CodeInstruction> code,string label)
    {
        bool threw=false;
        try { SummonTraining.VisibilityPatch.Transpiler(code); }
        catch(InvalidOperationException) { threw=true; }
        Check(threw,label);
    }
    internal static void Run()
    {
        Character s=Make("Skeleton_Friendly(Clone)",Character.Faction.PlayerSpawned,0);
        Character d=Make("piece_TrainingDummy(Clone)",Character.Faction.TrainingDummy,8);
        Character near=Make("piece_TrainingDummy",Character.Faction.TrainingDummy,4);
        Character enemy=Make("Greydwarf",Character.Faction.Undead,3);
        BaseAI ai=new BaseAI(); s.gameObject.Add(ai);
        Character.All.Clear(); Character.All.Add(d); Character.All.Add(near); Character.All.Add(enemy);
        SummonTraining.Active=false;
        Check(SummonTraining.SelectTarget(ai,s,null)==null,"disabled keeps vanilla selection");
        SummonTraining.Active=true;
        Check(SummonTraining.SelectTarget(ai,s,null)==near,"nearest dummy wins even when list is unsorted");
        Check(SummonTraining.SelectTarget(ai,s,enemy)==enemy,"vanilla ordinary enemy wins");
        ai.Target=enemy;
        Check(SummonTraining.SelectTarget(ai,s,null)==null,"existing ordinary enemy preserved");
        ai.Target=d;
        Check(SummonTraining.SelectTarget(ai,s,enemy)==enemy,"ordinary enemy replaces training target");
        ai.Target=null;
        ai.Visible=delegate(Character c) { return c!=near; };
        Check(SummonTraining.SelectTarget(ai,s,null)==d,"occluded nearest dummy skipped");
        ai.Visible=delegate { return false; };
        Check(SummonTraining.SelectTarget(ai,s,null)==null,"native visibility veto respected");
        ai.Visible=delegate { return true; };
        near.Dead=true;
        Check(SummonTraining.SelectTarget(ai,s,null)==d,"dead dummy skipped");
        d.Dead=true;
        Check(SummonTraining.SelectTarget(ai,s,null)==null,"no live training target");
        near.Dead=d.Dead=false;
        s.Owner=false;
        Check(SummonTraining.SelectTarget(ai,s,null)==null,"remote simulation owner untouched");
        s.Owner=true; s.Tamed=false;
        Check(!SummonTraining.Eligible(s),"wild skeleton excluded");
        s.Tamed=true; s.Dead=true;
        Check(!SummonTraining.Eligible(s),"dead skeleton excluded");
        s.Dead=false; s.Team=Character.Faction.Undead;
        Check(!SummonTraining.Eligible(s),"wrong faction excluded");
        s.Team=Character.Faction.PlayerSpawned; s.gameObject.name="Skeleton_Friendly_Custom(Clone)";
        Check(!SummonTraining.Eligible(s),"similar prefab names excluded");
        s.gameObject.name="Skeleton_Friendly(Clone)";
        Check(SummonTraining.Eligible(s),"summon scope restored");
        Check(!SummonTraining.Eligible(null),"null skeleton excluded");
        Check(!SummonTraining.IsDummy(enemy),"non-dummy excluded");
        d.Team=Character.Faction.Undead;
        Check(!SummonTraining.IsDummy(d),"dummy prefab with wrong faction excluded");
        d.Team=Character.Faction.TrainingDummy;

        Physics.NativeBlocked=true; Physics.Hits=new RaycastHit[]{Hit(d)};
        Check(!Blocked(s,d),"target own collider does not block");
        Physics.Hits=new RaycastHit[]{Hit(d),Hit(d)};
        Check(!Blocked(s,d),"multiple target colliders do not block");
        Physics.Hits=new RaycastHit[]{Hit(null),Hit(d)};
        Check(Blocked(s,d),"wall before target blocks");
        Physics.Hits=new RaycastHit[]{Hit(d),Hit(null)};
        Check(Blocked(s,d),"unsorted hits cannot hide a wall");
        Physics.Hits=new RaycastHit[]{Hit(d),Hit(near)};
        Check(Blocked(s,d),"different dummy still blocks");
        Physics.Hits=new RaycastHit[]{new RaycastHit()};
        Check(Blocked(s,d),"unknown collider fails closed");
        Physics.Hits=new RaycastHit[0];
        Check(!Blocked(s,d),"clear segment remains clear");
        Check(Physics.LastMask==1024 && Physics.LastDistance==17 && Physics.LastOrigin.y==2 && Physics.LastDirection.z==6,
            "native ray parameters retained");
        int all=Physics.AllCalls;
        SummonTraining.Active=false;
        Check(Blocked(s,d),"disabled uses native ray result");
        SummonTraining.Active=true; s.Owner=false;
        Check(Blocked(s,d),"ownership migration returns to native visibility");
        s.Owner=true;
        Check(Blocked(s,enemy),"ordinary enemy uses native ray result");
        Check(Physics.AllCalls==all,"ineligible calls do not use custom physics");
        s.gameObject.name="Wolf(Clone)";
        Check(Blocked(s,d),"other tamed creature retains native visibility");
        s.gameObject.name="Skeleton_Friendly(Clone)";
        Physics.NativeBlocked=false; SummonTraining.Active=false;
        Check(!Blocked(s,d),"native clear result preserved too");
        SummonTraining.Active=true;

        List<CodeInstruction> code=new List<CodeInstruction> {
            new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Ldarg_2), new CodeInstruction(OpCodes.Ldc_I4,1024),
            new CodeInstruction(OpCodes.Call,SummonTraining.RaycastMethod), new CodeInstruction(OpCodes.Ret) };
        List<CodeInstruction> rewritten=new List<CodeInstruction>(SummonTraining.VisibilityPatch.Transpiler(code));
        Check(rewritten.Count==8 && rewritten[4].opcode==OpCodes.Ldarg_0 && (byte)rewritten[5].operand==6,
            "transpiler supplies observer and exact target arguments");
        Check(rewritten[0]==code[0] && rewritten[7]==code[5],"surrounding native instructions preserved");
        DynamicMethod method=new DynamicMethod("Probe",typeof(bool),new Type[]{typeof(Transform),typeof(Vector3),
            typeof(float),typeof(float),typeof(bool),typeof(bool),typeof(Character)},typeof(SummonChecks),true);
        ILGenerator il=method.GetILGenerator();
        foreach(CodeInstruction i in rewritten)
        {
            if(i.operand==null) il.Emit(i.opcode);
            else if(i.operand is MethodInfo) il.Emit(i.opcode,(MethodInfo)i.operand);
            else if(i.operand is byte) il.Emit(i.opcode,(byte)i.operand);
            else il.Emit(i.opcode,(int)i.operand);
        }
        Physics.NativeBlocked=true; Physics.Hits=new RaycastHit[]{Hit(d)};
        object[] args={s.transform,new Vector3(1,2,3),17f,90f,true,false,d};
        Check(!(bool)method.Invoke(null,args),"emitted replacement executes with correct argument stack");
        s.Owner=false;
        Check((bool)method.Invoke(null,args),"emitted replacement preserves native nonowner result");
        s.Owner=true;
        Throws(new List<CodeInstruction>(),"missing ray layout rejected");
        Throws(new List<CodeInstruction>{new CodeInstruction(OpCodes.Call,SummonTraining.RaycastMethod),
            new CodeInstruction(OpCodes.Call,SummonTraining.RaycastMethod)},"ambiguous ray layout rejected");
        CodeInstruction exceptional=new CodeInstruction(OpCodes.Call,SummonTraining.RaycastMethod);
        exceptional.blocks.Add(new object());
        Throws(new List<CodeInstruction>{exceptional},"unexpected exception boundary rejected");
        CodeInstruction labelled=new CodeInstruction(OpCodes.Call,SummonTraining.RaycastMethod);
        labelled.labels.Add(il.DefineLabel());
        List<CodeInstruction> labels=new List<CodeInstruction>(SummonTraining.VisibilityPatch.Transpiler(new[]{labelled}));
        Check(labels[0].labels.Count==1 && labels[2].labels.Count==0,"branch labels move before added arguments");
        SummonTraining.Active=false; Character.All.Clear();
        Console.WriteLine("PASS: "+count+" summon behavior and emitted-IL assertions (simulated APIs, not Unity).");
    }
}
