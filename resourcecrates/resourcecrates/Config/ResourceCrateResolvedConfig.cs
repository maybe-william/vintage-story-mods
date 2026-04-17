using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using resourcecrates.Util;

namespace resourcecrates.Config
{
    public class ResourceCrateResolvedConfig
    {
        public double BaseTierRateMinutes { get; set; } = 180;
        public double LowerTierFactor { get; set; } = 10;
        public double HigherTierFactor { get; set; } = 100;

        /// <summary>
        /// Maps a generatable item code to its configured tier.
        /// Wildcards should already be expanded before insertion here.
        /// </summary>
        public Dictionary<AssetLocation, int> ItemTierByCode { get; set; } = new();

        /// <summary>
        /// Maps an upgrade item code to the crate tier it upgrades to.
        /// Example: game:ingot-iron -> 3
        /// These remain exact-only, no wildcard expansion.
        /// </summary>
        public Dictionary<AssetLocation, int> UpgradeTierByCode { get; set; } = new();

        public ResourceCrateResolvedConfig()
        {
            DebugLogger.Log("ResourceCrateResolvedConfig.ctor START");
            DebugLogger.Log("ResourceCrateResolvedConfig.ctor END");
        }

        public bool HasUpgradeItems
        {
            get
            {
                DebugLogger.Log("ResourceCrateResolvedConfig.HasUpgradeItems START");

                bool result = UpgradeTierByCode != null && UpgradeTierByCode.Count > 0;

                DebugLogger.Log($"ResourceCrateResolvedConfig.HasUpgradeItems END -> {result}");
                return result;
            }
        }

        public bool HasTierItems
        {
            get
            {
                DebugLogger.Log("ResourceCrateResolvedConfig.HasTierItems START");

                bool result = ItemTierByCode != null && ItemTierByCode.Count > 0;

                DebugLogger.Log($"ResourceCrateResolvedConfig.HasTierItems END -> {result}");
                return result;
            }
        }

        public int MaxTier
        {
            get
            {
                DebugLogger.Log("ResourceCrateResolvedConfig.MaxTier START");

                int result = 0;

                if (ItemTierByCode != null && ItemTierByCode.Count > 0)
                {
                    result = ItemTierByCode.Values.Max();
                }

                if (UpgradeTierByCode != null && UpgradeTierByCode.Count > 0)
                {
                    int maxUpgradeTier = UpgradeTierByCode.Values.Max();
                    if (maxUpgradeTier > result)
                    {
                        result = maxUpgradeTier;
                    }
                }

                DebugLogger.Log($"ResourceCrateResolvedConfig.MaxTier END -> {result}");
                return result;
            }
        }

        public bool TryGetItemTier(AssetLocation itemCode, out int tier)
        {
            DebugLogger.Log($"ResourceCrateResolvedConfig.TryGetItemTier START | itemCode={itemCode}");

            tier = -1;

            if (itemCode == null)
            {
                DebugLogger.Log("ResourceCrateResolvedConfig.TryGetItemTier END -> false (itemCode null)");
                return false;
            }

            bool result = ItemTierByCode != null && ItemTierByCode.TryGetValue(itemCode, out tier);

            DebugLogger.Log($"ResourceCrateResolvedConfig.TryGetItemTier END -> {result}, tier={tier}");
            return result;
        }

        public bool TryGetUpgradeTier(AssetLocation itemCode, out int tier)
        {
            DebugLogger.Log($"ResourceCrateResolvedConfig.TryGetUpgradeTier START | itemCode={itemCode}");

            tier = -1;

            if (itemCode == null)
            {
                DebugLogger.Log("ResourceCrateResolvedConfig.TryGetUpgradeTier END -> false (itemCode null)");
                return false;
            }

            bool result = UpgradeTierByCode != null && UpgradeTierByCode.TryGetValue(itemCode, out tier);

            DebugLogger.Log($"ResourceCrateResolvedConfig.TryGetUpgradeTier END -> {result}, tier={tier}");
            return result;
        }

        public bool IsGeneratableItem(AssetLocation itemCode)
        {
            DebugLogger.Log($"ResourceCrateResolvedConfig.IsGeneratableItem START | itemCode={itemCode}");

            bool result = itemCode != null &&
                          ItemTierByCode != null &&
                          ItemTierByCode.ContainsKey(itemCode);

            DebugLogger.Log($"ResourceCrateResolvedConfig.IsGeneratableItem END -> {result}");
            return result;
        }

        public bool IsUpgradeItem(AssetLocation itemCode)
        {
            DebugLogger.Log($"ResourceCrateResolvedConfig.IsUpgradeItem START | itemCode={itemCode}");

            bool result = itemCode != null &&
                          UpgradeTierByCode != null &&
                          UpgradeTierByCode.ContainsKey(itemCode);

            DebugLogger.Log($"ResourceCrateResolvedConfig.IsUpgradeItem END -> {result}");
            return result;
        }

        public override string ToString()
        {
            DebugLogger.Log("ResourceCrateResolvedConfig.ToString START");

            int itemTierCount = ItemTierByCode?.Count ?? 0;
            int upgradeTierCount = UpgradeTierByCode?.Count ?? 0;

            string result =
                $"BaseTierRateMinutes={BaseTierRateMinutes:0.###}, " +
                $"LowerTierFactor={LowerTierFactor:0.###}, " +
                $"HigherTierFactor={HigherTierFactor:0.###}, " +
                $"ItemTierByCode.Count={itemTierCount}, " +
                $"UpgradeTierByCode.Count={upgradeTierCount}, " +
                $"MaxTier={MaxTier}";

            DebugLogger.Log($"ResourceCrateResolvedConfig.ToString END -> {result}");
            return result;
        }
    }
}