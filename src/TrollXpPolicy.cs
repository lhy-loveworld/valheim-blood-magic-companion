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
}
