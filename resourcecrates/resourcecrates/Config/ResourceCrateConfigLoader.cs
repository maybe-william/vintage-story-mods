using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using resourcecrates.Util;

namespace resourcecrates.Config
{
    public class ResourceCrateConfigLoader
    {
        public const string DefaultConfigFileName = "resourcecrates.json";

        private readonly ICoreAPI api;
        private readonly string configFileName;

        public ResourceCrateConfigLoader(ICoreAPI api, string configFileName = DefaultConfigFileName)
        {
            DebugLogger.Log("ResourceCrateConfigLoader.ctor START");

            this.api = api ?? throw new ArgumentNullException(nameof(api));
            this.configFileName = string.IsNullOrWhiteSpace(configFileName)
                ? DefaultConfigFileName
                : configFileName;

            DebugLogger.Log($"ResourceCrateConfigLoader.ctor END | configFileName={this.configFileName}");
        }

        public ResourceCrateConfig LoadOrCreateRawConfig()
        {
            DebugLogger.Log("ResourceCrateConfigLoader.LoadOrCreateRawConfig START");

            ResourceCrateConfig config;

            try
            {
                config = api.LoadModConfig<ResourceCrateConfig>(configFileName);

                if (config == null)
                {
                    DebugLogger.Log("ResourceCrateConfigLoader.LoadOrCreateRawConfig | Config file not found, creating defaults");
                    config = CreateDefaultConfig();
                }

                ValidateRawConfig(config);

                api.StoreModConfig(config, configFileName);

                DebugLogger.Log($"ResourceCrateConfigLoader.LoadOrCreateRawConfig END | Loaded and stored config: {config}");
                return config;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"ResourceCrateConfigLoader.LoadOrCreateRawConfig | Failed to load config '{configFileName}': {ex}");

                config = CreateDefaultConfig();
                ValidateRawConfig(config);

                DebugLogger.Log($"ResourceCrateConfigLoader.LoadOrCreateRawConfig END | Returning default config after failure: {config}");
                return config;
            }
        }

        public ResourceCrateResolvedConfig LoadOrCreateResolvedConfig()
        {
            DebugLogger.Log("ResourceCrateConfigLoader.LoadOrCreateResolvedConfig START");

            ResourceCrateConfig rawConfig = LoadOrCreateRawConfig();
            ResourceCrateResolvedConfig resolvedConfig = ResolveConfig(rawConfig);

            DebugLogger.Log($"ResourceCrateConfigLoader.LoadOrCreateResolvedConfig END | resolvedConfig={resolvedConfig}");
            return resolvedConfig;
        }

        public ResourceCrateConfig CreateDefaultConfig()
        {
            DebugLogger.Log("ResourceCrateConfigLoader.CreateDefaultConfig START");

            ResourceCrateConfig config = new ResourceCrateConfig
            {
                BaseTierRateMinutes = 180,
                LowerTierFactor = 10,
                HigherTierFactor = 100,
                TierUpgradeItems = new List<string>
                {
                    "game:ingot-copper",
                    "game:ingot-tinbronze",
                    "game:ingot-iron",
                    "game:ingot-steel",
                    "game:crushed-ilmenite"
                },
                TierItems = new List<List<string>>
                {
                    new() { "game:log-grown-oak-ud" },
                    new() { "game:plank-oak" },
                    new() { "game:nugget-nativecopper" },
                    new() { "game:nugget-cassiterite" },
                    new() { "game:ironbloom" },
                    new() { "game:ingot-blistersteel" }
                }
            };

            DebugLogger.Log($"ResourceCrateConfigLoader.CreateDefaultConfig END | config={config}");
            return config;
        }

        public ResourceCrateResolvedConfig ResolveConfig(ResourceCrateConfig rawConfig)
        {
            DebugLogger.Log($"ResourceCrateConfigLoader.ResolveConfig START | rawConfig={rawConfig}");

            if (rawConfig == null)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ResolveConfig | rawConfig was null");
                throw new ArgumentNullException(nameof(rawConfig));
            }

            ValidateRawConfig(rawConfig);

            ResourceCrateResolvedConfig resolved = new ResourceCrateResolvedConfig
            {
                BaseTierRateMinutes = rawConfig.BaseTierRateMinutes,
                LowerTierFactor = rawConfig.LowerTierFactor,
                HigherTierFactor = rawConfig.HigherTierFactor
            };

            for (int i = 0; i < rawConfig.TierUpgradeItems.Count; i++)
            {
                string rawCode = rawConfig.TierUpgradeItems[i];
                int targetTier = i + 1;

                AssetLocation code = ParseAssetLocation(rawCode, $"tier_upgrade_items[{i}]");

                if (resolved.UpgradeTierByCode.ContainsKey(code))
                {
                    DebugLogger.Error($"ResourceCrateConfigLoader.ResolveConfig | Duplicate upgrade item code detected: {code}");
                    throw new InvalidOperationException($"Duplicate upgrade item code in config: {code}");
                }

                resolved.UpgradeTierByCode[code] = targetTier;
            }

            for (int tier = 0; tier < rawConfig.TierItems.Count; tier++)
            {
                List<string> tierEntries = rawConfig.TierItems[tier];

                if (tierEntries == null)
                {
                    DebugLogger.Error($"ResourceCrateConfigLoader.ResolveConfig | tier_items[{tier}] was null");
                    throw new InvalidOperationException($"tier_items[{tier}] cannot be null");
                }

                for (int j = 0; j < tierEntries.Count; j++)
                {
                    string rawCode = tierEntries[j];
                    AssetLocation code = ParseAssetLocation(rawCode, $"tier_items[{tier}][{j}]");

                    if (resolved.ItemTierByCode.ContainsKey(code))
                    {
                        DebugLogger.Error($"ResourceCrateConfigLoader.ResolveConfig | Duplicate generatable item code detected: {code}");
                        throw new InvalidOperationException($"Duplicate generatable item code in config: {code}");
                    }

                    resolved.ItemTierByCode[code] = tier;
                }
            }

            ValidateResolvedConfig(resolved);

            DebugLogger.Log($"ResourceCrateConfigLoader.ResolveConfig END | resolved={resolved}");
            return resolved;
        }

        public void ValidateRawConfig(ResourceCrateConfig config)
        {
            DebugLogger.Log($"ResourceCrateConfigLoader.ValidateRawConfig START | config={config}");

            if (config == null)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateRawConfig | config was null");
                throw new ArgumentNullException(nameof(config));
            }

            if (config.BaseTierRateMinutes <= 0)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateRawConfig | BaseTierRateMinutes must be > 0");
                throw new InvalidOperationException("base_tier_rate must be greater than 0");
            }

            if (config.LowerTierFactor <= 0)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateRawConfig | LowerTierFactor must be > 0");
                throw new InvalidOperationException("lower_tier_factor must be greater than 0");
            }

            if (config.HigherTierFactor <= 0)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateRawConfig | HigherTierFactor must be > 0");
                throw new InvalidOperationException("higher_tier_factor must be greater than 0");
            }

            if (config.TierUpgradeItems == null)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateRawConfig | TierUpgradeItems was null");
                throw new InvalidOperationException("tier_upgrade_items cannot be null");
            }

            if (config.TierItems == null)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateRawConfig | TierItems was null");
                throw new InvalidOperationException("tier_items cannot be null");
            }

            if (config.TierItems.Count == 0)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateRawConfig | TierItems was empty");
                throw new InvalidOperationException("tier_items must contain at least one tier");
            }

            for (int i = 0; i < config.TierUpgradeItems.Count; i++)
            {
                string rawCode = config.TierUpgradeItems[i];

                if (string.IsNullOrWhiteSpace(rawCode))
                {
                    DebugLogger.Error($"ResourceCrateConfigLoader.ValidateRawConfig | tier_upgrade_items[{i}] was blank");
                    throw new InvalidOperationException($"tier_upgrade_items[{i}] cannot be blank");
                }
            }

            for (int tier = 0; tier < config.TierItems.Count; tier++)
            {
                List<string> tierEntries = config.TierItems[tier];

                if (tierEntries == null)
                {
                    DebugLogger.Error($"ResourceCrateConfigLoader.ValidateRawConfig | tier_items[{tier}] was null");
                    throw new InvalidOperationException($"tier_items[{tier}] cannot be null");
                }

                for (int j = 0; j < tierEntries.Count; j++)
                {
                    string rawCode = tierEntries[j];

                    if (string.IsNullOrWhiteSpace(rawCode))
                    {
                        DebugLogger.Error($"ResourceCrateConfigLoader.ValidateRawConfig | tier_items[{tier}][{j}] was blank");
                        throw new InvalidOperationException($"tier_items[{tier}][{j}] cannot be blank");
                    }
                }
            }

            DebugLogger.Log("ResourceCrateConfigLoader.ValidateRawConfig END");
        }

        public void ValidateResolvedConfig(ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateConfigLoader.ValidateResolvedConfig START | config={config}");

            if (config == null)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateResolvedConfig | config was null");
                throw new ArgumentNullException(nameof(config));
            }

            if (config.BaseTierRateMinutes <= 0)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateResolvedConfig | BaseTierRateMinutes must be > 0");
                throw new InvalidOperationException("Resolved BaseTierRateMinutes must be greater than 0");
            }

            if (config.LowerTierFactor <= 0)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateResolvedConfig | LowerTierFactor must be > 0");
                throw new InvalidOperationException("Resolved LowerTierFactor must be greater than 0");
            }

            if (config.HigherTierFactor <= 0)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateResolvedConfig | HigherTierFactor must be > 0");
                throw new InvalidOperationException("Resolved HigherTierFactor must be greater than 0");
            }

            if (config.ItemTierByCode == null)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateResolvedConfig | ItemTierByCode was null");
                throw new InvalidOperationException("Resolved ItemTierByCode cannot be null");
            }

            if (config.UpgradeTierByCode == null)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.ValidateResolvedConfig | UpgradeTierByCode was null");
                throw new InvalidOperationException("Resolved UpgradeTierByCode cannot be null");
            }

            DebugLogger.Log("ResourceCrateConfigLoader.ValidateResolvedConfig END");
        }

        public AssetLocation ParseAssetLocation(string rawCode, string context)
        {
            DebugLogger.Log($"ResourceCrateConfigLoader.ParseAssetLocation START | context={context}, rawCode={rawCode}");

            if (string.IsNullOrWhiteSpace(rawCode))
            {
                DebugLogger.Error($"ResourceCrateConfigLoader.ParseAssetLocation | Blank code at {context}");
                throw new InvalidOperationException($"Blank asset code at {context}");
            }

            AssetLocation assetLocation;

            try
            {
                assetLocation = new AssetLocation(rawCode);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"ResourceCrateConfigLoader.ParseAssetLocation | Invalid asset code '{rawCode}' at {context}: {ex}");
                throw new InvalidOperationException($"Invalid asset code '{rawCode}' at {context}", ex);
            }

            DebugLogger.Log($"ResourceCrateConfigLoader.ParseAssetLocation END | assetLocation={assetLocation}");
            return assetLocation;
        }
    }
}