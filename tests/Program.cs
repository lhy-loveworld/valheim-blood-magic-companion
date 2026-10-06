internal static class Program
{
    private static int Main()
    {
        CostChecks.Run();
        SummonChecks.Run();
        return BloodMagicCompanion.XpChecks.Run();
    }
}
