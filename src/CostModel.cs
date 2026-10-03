using System;

namespace BloodMagicCompanion
{
    // The game owns affordability and deduction; this model supplies fixed stamina costs.
    internal static class CostModel
    {
        internal static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        internal static float Surcharge(float eitr, float skill, float rate, float reduction, float item)
        {
            if (!Finite(eitr) || !Finite(skill) || !Finite(rate) || !Finite(reduction) || !Finite(item) ||
                eitr < 0 || rate <= 0 || item <= 0 || reduction < 0 || reduction >= 1)
                return float.MaxValue;
            double discount = 1d - reduction * Math.Max(0d, Math.Min(1d, skill));
            double cost = (double)eitr * rate * item * discount;
            return (float)Math.Min(float.MaxValue, cost);
        }
        internal static float Total(float nativeStamina, float surcharge)
        {
            if (!Finite(nativeStamina) || !Finite(surcharge) || surcharge < 0)
                return float.MaxValue;
            // A native stamina refund cannot cancel the converted eitr charge.
            return (float)Math.Min(float.MaxValue, Math.Max(0d, nativeStamina) + surcharge);
        }
        internal static float Setting(float value, float minimum, float maximum, float fallback)
        {
            return Finite(value) && value >= minimum && value <= maximum ? value : fallback;
        }
    }
}
