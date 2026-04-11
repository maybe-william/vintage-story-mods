using resourcecrates.Config;
using resourcecrates.Util;

namespace resourcecrates.Domain
{
    public static class ResourceCrateTierMath
    {
        /// <summary>
        /// Calculates how many game minutes are required to generate 1 item,
        /// based on crate tier vs item tier.
        /// </summary>
        public static double GetMinutesPerItem(int crateTier, int itemTier, ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateTierMath.GetMinutesPerItem START | crateTier={crateTier}, itemTier={itemTier}");

            double result;

            if (crateTier == itemTier)
            {
                result = config.BaseTierRateMinutes;
            }
            else if (itemTier < crateTier)
            {
                int diff = crateTier - itemTier;
                result = config.BaseTierRateMinutes / Pow(config.LowerTierFactor, diff);
            }
            else
            {
                int diff = itemTier - crateTier;
                result = config.BaseTierRateMinutes * Pow(config.HigherTierFactor, diff);
            }

            DebugLogger.Log($"ResourceCrateTierMath.GetMinutesPerItem END -> {result}");
            return result;
        }

        /// <summary>
        /// Calculates how many items should be produced given elapsed minutes.
        /// Returns (itemsProduced, remainingProgressMinutes)
        /// </summary>
        public static (int items, double remainingProgress) ComputeProduction(
            double currentProgress,
            double minutesPerItem,
            double elapsedMinutes)
        {
            DebugLogger.Log($"ResourceCrateTierMath.ComputeProduction START | currentProgress={currentProgress}, minutesPerItem={minutesPerItem}, elapsedMinutes={elapsedMinutes}");

            double total = currentProgress + elapsedMinutes;

            if (minutesPerItem <= 0)
            {
                DebugLogger.Log("ResourceCrateTierMath.ComputeProduction END -> invalid minutesPerItem, returning 0");
                return (0, currentProgress);
            }

            int itemsProduced = (int)(total / minutesPerItem);
            double remaining = total % minutesPerItem;

            DebugLogger.Log($"ResourceCrateTierMath.ComputeProduction END -> items={itemsProduced}, remaining={remaining}");
            return (itemsProduced, remaining);
        }

        /// <summary>
        /// Safe exponent helper for doubles.
        /// </summary>
        public static double Pow(double baseValue, int exponent)
        {
            DebugLogger.Log($"ResourceCrateTierMath.Pow START | base={baseValue}, exponent={exponent}");

            double result = 1;

            for (int i = 0; i < exponent; i++)
            {
                result *= baseValue;
            }

            DebugLogger.Log($"ResourceCrateTierMath.Pow END -> {result}");
            return result;
        }

        /// <summary>
        /// Converts world total hours delta into game minutes.
        /// </summary>
        public static double HoursToMinutes(double hours)
        {
            DebugLogger.Log($"ResourceCrateTierMath.HoursToMinutes START | hours={hours}");

            double result = hours * 60.0;

            DebugLogger.Log($"ResourceCrateTierMath.HoursToMinutes END -> {result}");
            return result;
        }
    }
}