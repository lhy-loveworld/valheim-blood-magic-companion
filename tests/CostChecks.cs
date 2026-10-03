using System;
using BloodMagicCompanion;
internal static class CostChecks
{
    private static int count;
    private static void Check(bool condition, string label)
    { if (!condition) throw new Exception("FAILED: " + label); count++; }
    private static void Near(float actual, float expected, string label)
    { Check(Math.Abs(actual - expected) < 0.0001f, label + " actual=" + actual); }
    private static float Cost(float eitr=60, float skill=0, float rate=1, float reduction=0.33f, float item=1)
    { return CostModel.Surcharge(eitr,skill,rate,reduction,item); }
    public static void Run()
    {
        Near(Cost(),60,"shield level 0"); Near(Cost(skill:0.5f),50.1f,"shield level 50");
        Near(Cost(skill:1),40.2f,"shield level 100, discounted once");
        Near(Cost(eitr:100),100,"skeleton"); Near(Cost(eitr:120),120,"troll");
        Near(Cost(eitr:100,skill:1),67,"skeleton level 100");
        Near(Cost(skill:-2),60,"lower skill clamp"); Near(Cost(skill:5),40.2f,"upper skill clamp");
        Near(Cost(rate:0.5f),30,"conversion rate"); Near(Cost(item:2),120,"per-staff multiplier");
        Near(Cost(rate:2,item:0.5f),60,"combined configuration");
        Near(Cost(skill:1,reduction:0),60,"disable skill discount");
        Near(Cost(skill:1,reduction:0.5f),30,"custom skill discount"); Near(Cost(eitr:0),0,"no eitr");
        Near(CostModel.Total(10,Cost()),70,"existing native cost preserved");
        Near(CostModel.Total(6.7f,Cost(skill:1)),46.9f,"native cost not discounted twice");
        Near(CostModel.Total(-20,Cost()),60,"native refund cannot erase surcharge");
        Near(CostModel.Total(0,Cost()),60,"zero native stamina still costs stamina");
        Check(Cost(eitr:float.MaxValue,rate:100,item:100)==float.MaxValue,"overflow saturates");
        Check(CostModel.Total(float.MaxValue,Cost())==float.MaxValue,"addition saturates");
        foreach(float invalid in new float[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}) {
            Check(Cost(eitr:invalid)==float.MaxValue,"invalid eitr fails closed");
            Check(Cost(skill:invalid)==float.MaxValue,"invalid skill fails closed");
            Check(Cost(rate:invalid)==float.MaxValue,"invalid rate fails closed");
            Check(Cost(reduction:invalid)==float.MaxValue,"invalid discount fails closed");
            Check(Cost(item:invalid)==float.MaxValue,"invalid item fails closed");
            Check(CostModel.Total(invalid,60)==float.MaxValue,"invalid native cost fails closed");
            Check(CostModel.Total(0,invalid)==float.MaxValue,"invalid surcharge fails closed");
        }
        Check(Cost(eitr:-1)==float.MaxValue,"negative eitr");
        Check(Cost(rate:0)==float.MaxValue,"zero rate");
        Check(Cost(item:0)==float.MaxValue,"zero item multiplier");
        Check(Cost(reduction:-1)==float.MaxValue,"negative reduction");
        Check(Cost(reduction:1)==float.MaxValue,"free-casting discount rejected");
        Near(CostModel.Setting(float.NaN,0.01f,100,1),1,"NaN config fallback");
        Near(CostModel.Setting(0,0.01f,100,1),1,"out of range config fallback");
        Near(CostModel.Setting(0.75f,0.01f,100,1),0.75f,"valid config");
        float previous=Cost();
        for(int level=1;level<=100;level++) {
            float next=Cost(skill:level/100f);
            Check(next<=previous && next>0,"monotonic skill scaling " + level); previous=next;
        }
        foreach(float eitr in new float[]{60,100,120})
            foreach(float rate in new float[]{0.01f,1,100})
                foreach(float skill in new float[]{0,0.5f,1}) {
                    float cost=Cost(eitr:eitr,rate:rate,skill:skill);
                    Check(CostModel.Finite(cost) && cost>0,"positive fixed price across supported settings");
                    Near(CostModel.Total(7,cost),7+cost,"native plus converted cost");
                }
        Console.WriteLine("PASS: " + count + " stamina-cost regression assertions.");
    }
}
