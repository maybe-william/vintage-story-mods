using System;
using System.Collections.Generic;
using System.Linq;
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
                BaseTierRateMinutes = 5,
                LowerTierFactor = 5,
                HigherTierFactor = 50,
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
                    // Tier 0
                    new()
                    {
                        "game:log-*",
                        "game:clay-blue",
                        "game:clay-red",
                        "game:drygrass",
                        "game:stick",
                        "game:papyrustops",
                        "game:cattailtops",
                        "game:firewood",
                        "game:flint"
                    },

                    // Tier 1
                    new()
                    {
                        "game:plank-*",
                        
                        // rock all types minus bauxite
                        "game:rock-*",

                        // stone all types minus bauxite
                        "game:stone-*",

                        // "game:fat", // held item interaction
                        "game:lime",
                        "game:peatbrick",
                        "game:soil-low-*",
                        "game:calcined-flint"
                    },

                    // Tier 2
                    new()
                    {
                        "game:nugget-nativecopper",
                        "game:nugget-malachite",
                        "game:rock-bauxite",
                        "game:stone-bauxite",
                        "game:clearquartz",
                        "game:smokyquartz",
                        "game:ore-quartz",
                        "game:resin",
                        "game:charcoal",
                        "game:clay-fire",
                        "game:soil-medium-*"
                    },

                    // Tier 3
                    new()
                    {
                        "game:nugget-cassiterite",
                        "game:nugget-sphalerite",
                        "game:nugget-bismuthinite",
                        "game:ore-olivine",
                        "game:nugget-nativesilver",
                        "game:nugget-galena",
                        "game:ore-sulfur",
                        "game:saltpeter",
                        "game:soil-high-*",
                        "game:ore-lignite"
                    },

                    // Tier 4
                    new()
                    {
                        "game:ironbloom",
                        "game:nugget-hematite",
                        "game:nugget-limonite",
                        "game:nugget-magnetite",
                        "game:nugget-nativegold",
                        // "game:gear-rusty", // held item interaction
                        "game:ore-bituminouscoal",
                        "game:rot"
                    },

                    // Tier 5
                    new()
                    {
                        "game:ingot-blistersteel",
                        "game:nugget-ilmenite",
                        "game:nugget-chromite",
                        // "game:gear-temporal", // held item interaction
                        "game:jonas*"
                    }
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

            // Upgrade items stay exact-only.
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

            // Tier items support wildcard expansion.
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
                    string context = $"tier_items[{tier}][{j}]";

                    foreach (AssetLocation code in ExpandTierItemEntry(rawCode, context))
                    {
                        AddOrPromoteGeneratableItemTier(resolved.ItemTierByCode, code, tier, context);
                    }
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

                if (ContainsWildcard(rawCode))
                {
                    DebugLogger.Error($"ResourceCrateConfigLoader.ValidateRawConfig | tier_upgrade_items[{i}] cannot use wildcard syntax: {rawCode}");
                    throw new InvalidOperationException($"tier_upgrade_items[{i}] cannot use wildcard syntax");
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

                    ValidateTierItemEntrySyntax(rawCode, $"tier_items[{tier}][{j}]");
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

        private List<AssetLocation> ExpandTierItemEntry(string rawCode, string context)
        {
            DebugLogger.Log($"ResourceCrateConfigLoader.ExpandTierItemEntry START | context={context}, rawCode={rawCode}");

            if (!ContainsWildcard(rawCode))
            {
                AssetLocation exact = ParseAssetLocation(rawCode, context);
                DebugLogger.Log($"ResourceCrateConfigLoader.ExpandTierItemEntry END | exact match only: {exact}");
                return new List<AssetLocation> { exact };
            }

            AssetLocation pattern = ParseWildcardAssetLocation(rawCode, context);
            string domain = pattern.Domain;
            string pathPattern = pattern.Path;

            if (!pathPattern.EndsWith("*"))
            {
                DebugLogger.Error($"ResourceCrateConfigLoader.ExpandTierItemEntry | Wildcard must be suffix-only at {context}: {rawCode}");
                throw new InvalidOperationException($"Wildcard must be suffix-only at {context}: {rawCode}");
            }

            string prefix = pathPattern.Substring(0, pathPattern.Length - 1);
            List<AssetLocation> matches = new();

            foreach (CollectibleObject collectible in api.World.Collectibles)
            {
                if (collectible?.Code == null) continue;

                AssetLocation code = collectible.Code;

                if (!string.Equals(code.Domain, domain, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!code.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matches.Add(code);
            }

            matches = matches
                .Distinct()
                .OrderBy(c => c.ToShortString())
                .ToList();
            
            foreach (var match in matches)
            {
                DebugLogger.Log($"[WildcardExpand] {rawCode} -> {match}");
            }
            
            if (matches.Count == 0)
            {
                DebugLogger.Error($"ResourceCrateConfigLoader.ExpandTierItemEntry | Wildcard entry matched no collectibles at {context}: {rawCode}");
                throw new InvalidOperationException($"Wildcard entry matched no collectibles at {context}: {rawCode}");
            }

            DebugLogger.Log($"ResourceCrateConfigLoader.ExpandTierItemEntry END | expanded {rawCode} to {matches.Count} matches");
            return matches;
        }

        private void AddOrPromoteGeneratableItemTier(
            Dictionary<AssetLocation, int> itemTierByCode,
            AssetLocation code,
            int newTier,
            string context)
        {
            DebugLogger.Log($"ResourceCrateConfigLoader.AddOrPromoteGeneratableItemTier START | context={context}, code={code}, newTier={newTier}");

            if (itemTierByCode == null)
            {
                DebugLogger.Error("ResourceCrateConfigLoader.AddOrPromoteGeneratableItemTier | itemTierByCode was null");
                throw new ArgumentNullException(nameof(itemTierByCode));
            }

            if (code == null)
            {
                DebugLogger.Error($"ResourceCrateConfigLoader.AddOrPromoteGeneratableItemTier | code was null at {context}");
                throw new ArgumentNullException(nameof(code));
            }

            if (itemTierByCode.TryGetValue(code, out int existingTier))
            {
                if (newTier > existingTier)
                {
                    itemTierByCode[code] = newTier;
                    DebugLogger.Log($"ResourceCrateConfigLoader.AddOrPromoteGeneratableItemTier | Promoted duplicate item {code} from tier {existingTier} to {newTier} (source: {context})");
                }
                else
                {
                    DebugLogger.Log($"ResourceCrateConfigLoader.AddOrPromoteGeneratableItemTier | Ignored duplicate item {code} at tier {newTier}, keeping existing tier {existingTier} (source: {context})");
                }

                DebugLogger.Log("ResourceCrateConfigLoader.AddOrPromoteGeneratableItemTier END | duplicate handled");
                return;
            }

            itemTierByCode[code] = newTier;
            DebugLogger.Log($"ResourceCrateConfigLoader.AddOrPromoteGeneratableItemTier END | added new item {code} at tier {newTier}");
        }

        private AssetLocation ParseWildcardAssetLocation(string rawCode, string context)
        {
            DebugLogger.Log($"ResourceCrateConfigLoader.ParseWildcardAssetLocation START | context={context}, rawCode={rawCode}");

            if (string.IsNullOrWhiteSpace(rawCode))
            {
                DebugLogger.Error($"ResourceCrateConfigLoader.ParseWildcardAssetLocation | Blank code at {context}");
                throw new InvalidOperationException($"Blank wildcard asset code at {context}");
            }

            int colonIndex = rawCode.IndexOf(':');
            if (colonIndex <= 0 || colonIndex == rawCode.Length - 1)
            {
                DebugLogger.Error($"ResourceCrateConfigLoader.ParseWildcardAssetLocation | Invalid wildcard code '{rawCode}' at {context}");
                throw new InvalidOperationException($"Invalid wildcard code '{rawCode}' at {context}");
            }

            string domain = rawCode.Substring(0, colonIndex);
            string path = rawCode.Substring(colonIndex + 1);

            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(path))
            {
                DebugLogger.Error($"ResourceCrateConfigLoader.ParseWildcardAssetLocation | Invalid wildcard code '{rawCode}' at {context}");
                throw new InvalidOperationException($"Invalid wildcard code '{rawCode}' at {context}");
            }

            if (path.Count(c => c == '*') > 1)
            {
                DebugLogger.Error($"ResourceCrateConfigLoader.ParseWildcardAssetLocation | Only one wildcard is supported at {context}: {rawCode}");
                throw new InvalidOperationException($"Only one wildcard is supported at {context}: {rawCode}");
            }

            if (path.Contains('*') && !path.EndsWith("*"))
            {
                DebugLogger.Error($"ResourceCrateConfigLoader.ParseWildcardAssetLocation | Wildcard must be suffix-only at {context}: {rawCode}");
                throw new InvalidOperationException($"Wildcard must be suffix-only at {context}: {rawCode}");
            }

            AssetLocation result = new AssetLocation(domain, path);

            DebugLogger.Log($"ResourceCrateConfigLoader.ParseWildcardAssetLocation END | assetLocation={result}");
            return result;
        }

        private bool ContainsWildcard(string rawCode)
        {
            return !string.IsNullOrWhiteSpace(rawCode) && rawCode.Contains('*');
        }

        private void ValidateTierItemEntrySyntax(string rawCode, string context)
        {
            DebugLogger.Log($"ResourceCrateConfigLoader.ValidateTierItemEntrySyntax START | context={context}, rawCode={rawCode}");

            if (!ContainsWildcard(rawCode))
            {
                ParseAssetLocation(rawCode, context);
                DebugLogger.Log("ResourceCrateConfigLoader.ValidateTierItemEntrySyntax END | exact syntax valid");
                return;
            }

            ParseWildcardAssetLocation(rawCode, context);
            DebugLogger.Log("ResourceCrateConfigLoader.ValidateTierItemEntrySyntax END | wildcard syntax valid");
        }
    }
}