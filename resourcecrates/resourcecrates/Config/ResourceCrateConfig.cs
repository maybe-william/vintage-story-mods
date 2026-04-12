using System.Collections.Generic;
using resourcecrates.Util;

namespace resourcecrates.Config
{
    public class ResourceCrateConfig
    {
        public double BaseTierRateMinutes { get; set; } = 10;

        public double LowerTierFactor { get; set; } = 5;

        public double HigherTierFactor { get; set; } = 50;

        public List<string> TierUpgradeItems { get; set; } = new();

        public List<List<string>> TierItems { get; set; } = new();

        public ResourceCrateConfig()
        {
            DebugLogger.Log("ResourceCrateConfig.ctor START");
            DebugLogger.Log("ResourceCrateConfig.ctor END");
        }

        public bool HasUpgradeItems
        {
            get
            {
                DebugLogger.Log("ResourceCrateConfig.HasUpgradeItems START");
                bool result = TierUpgradeItems != null && TierUpgradeItems.Count > 0;
                DebugLogger.Log($"ResourceCrateConfig.HasUpgradeItems END -> {result}");
                return result;
            }
        }

        public bool HasTierItems
        {
            get
            {
                DebugLogger.Log("ResourceCrateConfig.HasTierItems START");
                bool result = TierItems != null && TierItems.Count > 0;
                DebugLogger.Log($"ResourceCrateConfig.HasTierItems END -> {result}");
                return result;
            }
        }

        public int MaxTier
        {
            get
            {
                DebugLogger.Log("ResourceCrateConfig.MaxTier START");

                int result = TierItems == null || TierItems.Count == 0
                    ? 0
                    : TierItems.Count - 1;

                DebugLogger.Log($"ResourceCrateConfig.MaxTier END -> {result}");
                return result;
            }
        }

        public override string ToString()
        {
            DebugLogger.Log("ResourceCrateConfig.ToString START");

            int upgradeCount = TierUpgradeItems?.Count ?? 0;
            int tierGroupCount = TierItems?.Count ?? 0;

            string result =
                $"BaseTierRateMinutes={BaseTierRateMinutes:0.###}, " +
                $"LowerTierFactor={LowerTierFactor:0.###}, " +
                $"HigherTierFactor={HigherTierFactor:0.###}, " +
                $"TierUpgradeItems.Count={upgradeCount}, " +
                $"TierItems.Count={tierGroupCount}";

            DebugLogger.Log($"ResourceCrateConfig.ToString END -> {result}");
            return result;
        }
    }
}