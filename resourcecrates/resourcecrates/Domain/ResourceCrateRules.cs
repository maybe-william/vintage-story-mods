using Vintagestory.API.Common;
using resourcecrates.Config;
using resourcecrates.Util;

namespace resourcecrates.Domain
{
    public static class ResourceCrateRules
    {
        /// <summary>
        /// Returns true if the given held item can upgrade the crate.
        /// A valid upgrade item upgrades the crate directly to that item's configured tier,
        /// but only if that target tier is strictly greater than the current crate tier.
        /// </summary>
        public static bool CanUpgrade(ResourceCrateState state, ItemStack? heldStack, ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateRules.CanUpgrade START | state={state}, heldStack={(heldStack == null ? "null" : heldStack.Collectible?.Code?.ToString() ?? "null")}");

            bool result = false;

            if (state != null && heldStack != null && heldStack.Collectible?.Code != null && config != null)
            {
                if (config.TryGetUpgradeTier(heldStack.Collectible.Code, out int upgradeTier))
                {
                    result = upgradeTier > state.CrateTier;
                }
            }

            DebugLogger.Log($"ResourceCrateRules.CanUpgrade END -> {result}");
            return result;
        }

        /// <summary>
        /// Returns the target crate tier that the held upgrade item would set,
        /// or -1 if the held item is not a valid upgrade or would not improve the crate.
        /// </summary>
        public static int GetUpgradeTargetTier(ResourceCrateState state, ItemStack? heldStack, ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateRules.GetUpgradeTargetTier START | state={state}, heldStack={(heldStack == null ? "null" : heldStack.Collectible?.Code?.ToString() ?? "null")}");

            int result = -1;

            if (state != null && heldStack != null && heldStack.Collectible?.Code != null && config != null)
            {
                if (config.TryGetUpgradeTier(heldStack.Collectible.Code, out int upgradeTier) && upgradeTier > state.CrateTier)
                {
                    result = upgradeTier;
                }
            }

            DebugLogger.Log($"ResourceCrateRules.GetUpgradeTargetTier END -> {result}");
            return result;
        }

        /// <summary>
        /// Returns true if the held item can be assigned as the crate's generation target.
        /// The item must be listed as generatable, and the crate must not already have a target.
        /// </summary>
        public static bool CanAssignTarget(ResourceCrateState state, ItemStack? heldStack, ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateRules.CanAssignTarget START | state={state}, heldStack={(heldStack == null ? "null" : heldStack.Collectible?.Code?.ToString() ?? "null")}");

            bool result = false;

            if (state != null && heldStack != null && heldStack.Collectible?.Code != null && config != null)
            {
                result = !state.HasTargetItem && config.IsGeneratableItem(heldStack.Collectible.Code);
            }

            DebugLogger.Log($"ResourceCrateRules.CanAssignTarget END -> {result}");
            return result;
        }

        /// <summary>
        /// Returns true if the crate's target may be cleared or changed.
        /// For now, this allows retargeting only when the output slot is empty.
        /// </summary>
        public static bool CanRetarget(ResourceCrateState state, ItemStack? currentStoredStack)
        {
            DebugLogger.Log($"ResourceCrateRules.CanRetarget START | state={state}, currentStoredStack={(currentStoredStack == null ? "null" : currentStoredStack.Collectible?.Code?.ToString() ?? "null")}");

            bool result = false;

            if (state != null)
            {
                result = currentStoredStack == null || currentStoredStack.StackSize <= 0;
            }

            DebugLogger.Log($"ResourceCrateRules.CanRetarget END -> {result}");
            return result;
        }

        /// <summary>
        /// Returns true if the held item can replace the existing target.
        /// For now, replacement is allowed only if the crate can retarget and the held item is generatable.
        /// </summary>
        public static bool CanReplaceTarget(ResourceCrateState state, ItemStack? heldStack, ItemStack? currentStoredStack, ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateRules.CanReplaceTarget START | state={state}, heldStack={(heldStack == null ? "null" : heldStack.Collectible?.Code?.ToString() ?? "null")}, currentStoredStack={(currentStoredStack == null ? "null" : currentStoredStack.Collectible?.Code?.ToString() ?? "null")}");

            bool result = false;

            if (state != null && heldStack != null && heldStack.Collectible?.Code != null && config != null)
            {
                result = CanRetarget(state, currentStoredStack) && config.IsGeneratableItem(heldStack.Collectible.Code);
            }

            DebugLogger.Log($"ResourceCrateRules.CanReplaceTarget END -> {result}");
            return result;
        }

        /// <summary>
        /// Returns true if the held item is a valid generatable item in config.
        /// </summary>
        public static bool IsValidTargetItem(ItemStack? heldStack, ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateRules.IsValidTargetItem START | heldStack={(heldStack == null ? "null" : heldStack.Collectible?.Code?.ToString() ?? "null")}");

            bool result = false;

            if (heldStack != null && heldStack.Collectible?.Code != null && config != null)
            {
                result = config.IsGeneratableItem(heldStack.Collectible.Code);
            }

            DebugLogger.Log($"ResourceCrateRules.IsValidTargetItem END -> {result}");
            return result;
        }

        /// <summary>
        /// Returns true if the held item is a configured upgrade item.
        /// </summary>
        public static bool IsValidUpgradeItem(ItemStack? heldStack, ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateRules.IsValidUpgradeItem START | heldStack={(heldStack == null ? "null" : heldStack.Collectible?.Code?.ToString() ?? "null")}");

            bool result = false;

            if (heldStack != null && heldStack.Collectible?.Code != null && config != null)
            {
                result = config.IsUpgradeItem(heldStack.Collectible.Code);
            }

            DebugLogger.Log($"ResourceCrateRules.IsValidUpgradeItem END -> {result}");
            return result;
        }

        /// <summary>
        /// Returns true if a successful upgrade should consume one held item.
        /// Current rule: yes.
        /// </summary>
        public static bool ShouldConsumeUpgradeItem(ResourceCrateState state, ItemStack? heldStack, ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateRules.ShouldConsumeUpgradeItem START | state={state}, heldStack={(heldStack == null ? "null" : heldStack.Collectible?.Code?.ToString() ?? "null")}");

            bool result = CanUpgrade(state, heldStack, config);

            DebugLogger.Log($"ResourceCrateRules.ShouldConsumeUpgradeItem END -> {result}");
            return result;
        }

        /// <summary>
        /// Returns true if a successful target assignment should consume one held item.
        /// Current rule: no.
        /// </summary>
        public static bool ShouldConsumeTargetItem(ResourceCrateState state, ItemStack? heldStack, ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateRules.ShouldConsumeTargetItem START | state={state}, heldStack={(heldStack == null ? "null" : heldStack.Collectible?.Code?.ToString() ?? "null")}");

            bool result = false;

            DebugLogger.Log($"ResourceCrateRules.ShouldConsumeTargetItem END -> {result}");
            return result;
        }

        /// <summary>
        /// Returns true if the crate is configured enough to generate.
        /// This only checks state/config prerequisites, not inventory room.
        /// </summary>
        public static bool CanGenerate(ResourceCrateState state, ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateRules.CanGenerate START | state={state}");

            bool result = false;

            if (state != null && config != null && state.TargetItemCode != null)
            {
                result = config.TryGetItemTier(state.TargetItemCode, out _);
            }

            DebugLogger.Log($"ResourceCrateRules.CanGenerate END -> {result}");
            return result;
        }

        /// <summary>
        /// Returns the configured tier of the crate's current target item,
        /// or -1 if no valid target is assigned.
        /// </summary>
        public static int GetTargetItemTier(ResourceCrateState state, ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateRules.GetTargetItemTier START | state={state}");

            int result = -1;

            if (state != null && config != null && state.TargetItemCode != null)
            {
                if (config.TryGetItemTier(state.TargetItemCode, out int tier))
                {
                    result = tier;
                }
            }

            DebugLogger.Log($"ResourceCrateRules.GetTargetItemTier END -> {result}");
            return result;
        }

        /// <summary>
        /// Returns the current generation time in game minutes for the crate's configured target item,
        /// or -1 if generation is not currently valid.
        /// </summary>
        public static double GetMinutesPerItem(ResourceCrateState state, ResourceCrateResolvedConfig config)
        {
            DebugLogger.Log($"ResourceCrateRules.GetMinutesPerItem START | state={state}");

            double result = -1;

            if (CanGenerate(state, config))
            {
                int itemTier = GetTargetItemTier(state, config);
                if (itemTier >= 0)
                {
                    result = ResourceCrateTierMath.GetMinutesPerItem(state.CrateTier, itemTier, config);
                }
            }

            DebugLogger.Log($"ResourceCrateRules.GetMinutesPerItem END -> {result}");
            return result;
        }

        /// <summary>
        /// Returns true if the crate has meaningful metadata that should be preserved
        /// when converting to an itemstack on break.
        /// </summary>
        public static bool ShouldPreserveMetadata(ResourceCrateState state)
        {
            DebugLogger.Log($"ResourceCrateRules.ShouldPreserveMetadata START | state={state}");

            bool result = false;

            if (state != null)
            {
                result = !state.IsUninitialized;
            }

            DebugLogger.Log($"ResourceCrateRules.ShouldPreserveMetadata END -> {result}");
            return result;
        }
    }
}