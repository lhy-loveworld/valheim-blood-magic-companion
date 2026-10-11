internal static class Program
{
    private static int Main()
    {
        CostChecks.Run();
        SummonChecks.Run();
        LavaChecks.Run();
        TrollChecks.Run();
        return BloodMagicCompanion.XpChecks.Run();
    }
}
