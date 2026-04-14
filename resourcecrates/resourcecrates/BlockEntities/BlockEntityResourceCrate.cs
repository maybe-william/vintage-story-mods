using System;
using System.Text;
using resourcecrates.Config;
using resourcecrates.Domain;
using resourcecrates.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace resourcecrates.BlockEntities
{
    public class BlockEntityResourceCrate : BlockEntityGenericTypedContainer
    {
        private const string TreeAttrCrateTier = "resourcecrates:crateTier";
        private const string TreeAttrTargetItemCode = "resourcecrates:targetItemCode";
        private const string TreeAttrProgressMinutes = "resourcecrates:progressMinutes";
        private const string TreeAttrLastUpdateTotalHours = "resourcecrates:lastUpdateTotalHours";

        private const string StackAttrTreeName = "resourcecratesState";
        private const double MaxStoredProgressSeconds = 1_000_000_000d;
        private const double MaxStoredProgressMinutes = MaxStoredProgressSeconds / 60d;

        private long serverTickListenerId = 0;
        private ResourceCrateState state = new ResourceCrateState();

        public ResourceCrateState State => state;

        public ItemSlot OutputSlot => Inventory?[0]; // conflict here

        private ResourceCrateResolvedConfig ResolvedConfig
            => resourcecratesModSystem.GetResolvedConfigOrThrow();

        public override void Initialize(ICoreAPI api)
        {
            DebugLogger.Log("BlockEntityResourceCrate.Initialize START");

            base.Initialize(api); // conflict here

            if (api.Side == EnumAppSide.Server)
            {
                EnsureLastUpdateInitialized();
                serverTickListenerId = RegisterGameTickListener(OnServerTick, 1000); // conflict here
                DebugLogger.Log($"BlockEntityResourceCrate.Initialize | Registered tick listener id={serverTickListenerId}");
            }

            DebugLogger.Log("BlockEntityResourceCrate.Initialize END");
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.OnBlockPlaced START | byItemStack={byItemStack?.Collectible?.Code}");

            base.OnBlockPlaced(byItemStack); // conflict here

            if (byItemStack != null)
            {
                ReadCrateStateFromItemStack(byItemStack);
            }

            EnsureLastUpdateInitialized();
            MarkDirty(true); // conflict here

            DebugLogger.Log($"BlockEntityResourceCrate.OnBlockPlaced END | state={state}");
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            DebugLogger.Log("BlockEntityResourceCrate.FromTreeAttributes START");

            base.FromTreeAttributes(tree, worldForResolving); // conflict here

            state.CrateTier = tree.GetInt(TreeAttrCrateTier, 0);
            state.ProgressMinutes = ClampProgressMinutes(tree.GetDouble(TreeAttrProgressMinutes, 0));
            state.LastUpdateTotalHours = tree.GetDouble(TreeAttrLastUpdateTotalHours, 0);

            string targetCode = tree.GetString(TreeAttrTargetItemCode, null);
            state.TargetItemCode = ParseAssetLocationOrNull(targetCode);

            DebugLogger.Log($"BlockEntityResourceCrate.FromTreeAttributes END | state={state}");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.ToTreeAttributes START | state={state}");

            base.ToTreeAttributes(tree); // conflict here

            tree.SetInt(TreeAttrCrateTier, state.CrateTier);
            tree.SetDouble(TreeAttrProgressMinutes, ClampProgressMinutes(state.ProgressMinutes));
            tree.SetDouble(TreeAttrLastUpdateTotalHours, state.LastUpdateTotalHours);

            if (state.TargetItemCode == null)
            {
                tree.RemoveAttribute(TreeAttrTargetItemCode);
            }
            else
            {
                tree.SetString(TreeAttrTargetItemCode, state.TargetItemCode.ToShortString());
            }

            DebugLogger.Log("BlockEntityResourceCrate.ToTreeAttributes END");
        }

        public override void OnBlockUnloaded()
        {
            DebugLogger.Log("BlockEntityResourceCrate.OnBlockUnloaded START");

            base.OnBlockUnloaded(); // conflict here

            if (serverTickListenerId != 0)
            {
                UnregisterGameTickListener(serverTickListenerId); // conflict here
                serverTickListenerId = 0;
            }

            DebugLogger.Log("BlockEntityResourceCrate.OnBlockUnloaded END");
        }

        public override void OnBlockRemoved()
        {
            DebugLogger.Log("BlockEntityResourceCrate.OnBlockRemoved START");

            base.OnBlockRemoved(); // conflict here

            if (serverTickListenerId != 0)
            {
                UnregisterGameTickListener(serverTickListenerId); // conflict here
                serverTickListenerId = 0;
            }

            DebugLogger.Log("BlockEntityResourceCrate.OnBlockRemoved END");
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc); // conflict here

            dsc.AppendLine(Lang.Get("Tier: {0}", state.CrateTier));
            dsc.AppendLine(Lang.Get("Target: {0}", state.TargetItemCode?.ToShortString() ?? "none"));

            if (state.TargetItemCode != null)
            {
                try
                {
                    double minutesPerItem = ResourceCrateRules.GetMinutesPerItem(state, ResolvedConfig);
                    if (minutesPerItem > 0)
                    {
                        dsc.AppendLine(Lang.Get("Rate: {0:0.###} in-game minutes/item", minutesPerItem));
                    }
                }
                catch
                {
                    dsc.AppendLine(Lang.Get("Rate: invalid"));
                }
            }

            dsc.AppendLine(Lang.Get("Stored progress: {0:0.###} minutes", state.ProgressMinutes));
        }

        public bool TryUpgrade(IPlayer byPlayer, ItemSlot handSlot)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.TryUpgrade START | player={byPlayer?.PlayerName}, handSlot={handSlot?.Itemstack?.Collectible?.Code}, state={state}");

            if (Api == null || Api.Side != EnumAppSide.Server || byPlayer == null || handSlot?.Itemstack == null) // conflict here
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryUpgrade END -> false (invalid preconditions)");
                return false;
            }

            ResourceCrateResolvedConfig config = ResolvedConfig;
            ItemStack heldStack = handSlot.Itemstack;

            if (!ResourceCrateRules.CanUpgrade(state, heldStack, config))
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryUpgrade END -> false (rules denied)");
                return false;
            }

            int targetTier = ResourceCrateRules.GetUpgradeTargetTier(state, heldStack, config);
            if (targetTier < 0)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryUpgrade END -> false (no valid target tier)");
                return false;
            }

            state.CrateTier = targetTier;

            // v0 rule: upgrading is allowed even with output present,
            // and existing output simply disappears.
            ClearOutputForUpgrade();

            if (ResourceCrateRules.ShouldConsumeUpgradeItem(state, heldStack, config))
            {
                handSlot.TakeOut(1);
                handSlot.MarkDirty();
            }

            EnsureLastUpdateInitialized();
            MarkDirty(true); // conflict here

            DebugLogger.Log($"BlockEntityResourceCrate.TryUpgrade END -> true | newState={state}");
            return true;
        }

        public bool TrySetOrReplaceTarget(IPlayer byPlayer, ItemSlot handSlot)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.TrySetOrReplaceTarget START | player={byPlayer?.PlayerName}, handSlot={handSlot?.Itemstack?.Collectible?.Code}, state={state}");

            if (Api == null || Api.Side != EnumAppSide.Server || byPlayer == null || handSlot?.Itemstack == null) // conflict here
            {
                DebugLogger.Log("BlockEntityResourceCrate.TrySetOrReplaceTarget END -> false (invalid preconditions)");
                return false;
            }

            ResourceCrateResolvedConfig config = ResolvedConfig;
            ItemStack heldStack = handSlot.Itemstack;

            bool canChange =
                !state.HasTargetItem
                    ? ResourceCrateRules.CanAssignTarget(state, heldStack, config)
                    : ResourceCrateRules.CanReplaceTarget(state, heldStack, OutputSlot?.Itemstack, config);

            if (!canChange)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TrySetOrReplaceTarget END -> false (rules denied)");
                return false;
            }

            SetTarget(heldStack.Collectible.Code);

            if (ResourceCrateRules.ShouldConsumeTargetItem(state, heldStack, config))
            {
                handSlot.TakeOut(1);
                handSlot.MarkDirty();
            }

            EnsureLastUpdateInitialized();
            MarkDirty(true); // conflict here

            DebugLogger.Log($"BlockEntityResourceCrate.TrySetOrReplaceTarget END -> true | newState={state}");
            return true;
        }

        public void SetTarget(AssetLocation targetCode)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.SetTarget START | targetCode={targetCode}, beforeState={state}");

            if (targetCode == null)
            {
                throw new ArgumentNullException(nameof(targetCode));
            }

            bool changed = state.TargetItemCode == null || !state.TargetItemCode.Equals(targetCode);

            state.TargetItemCode = targetCode.Clone();

            // Conservative v0 behavior:
            // retargeting clears accumulated progress, but leaves current slot contents alone.
            if (changed)
            {
                state.ProgressMinutes = 0;
            }

            DebugLogger.Log($"BlockEntityResourceCrate.SetTarget END | afterState={state}");
        }

        public void ClearOutputForUpgrade()
        {
            DebugLogger.Log($"BlockEntityResourceCrate.ClearOutputForUpgrade START | output={OutputSlot?.Itemstack?.Collectible?.Code}");

            if (OutputSlot != null && !OutputSlot.Empty)
            {
                OutputSlot.Itemstack = null;
                OutputSlot.MarkDirty();
            }

            DebugLogger.Log("BlockEntityResourceCrate.ClearOutputForUpgrade END");
        }

        public void WriteCrateStateToItemStack(ItemStack stack)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.WriteCrateStateToItemStack START | stack={stack?.Collectible?.Code}, state={state}");

            if (stack == null)
            {
                return;
            }

            if (!ResourceCrateRules.ShouldPreserveMetadata(state))
            {
                stack.Attributes.RemoveAttribute(StackAttrTreeName);
                DebugLogger.Log("BlockEntityResourceCrate.WriteCrateStateToItemStack END | metadata removed");
                return;
            }

            TreeAttribute crateTree = new TreeAttribute();
            crateTree.SetInt(TreeAttrCrateTier, state.CrateTier);
            crateTree.SetDouble(TreeAttrProgressMinutes, ClampProgressMinutes(state.ProgressMinutes));

            if (state.TargetItemCode != null)
            {
                crateTree.SetString(TreeAttrTargetItemCode, state.TargetItemCode.ToShortString());
            }

            stack.Attributes[StackAttrTreeName] = crateTree;

            DebugLogger.Log("BlockEntityResourceCrate.WriteCrateStateToItemStack END | metadata written");
        }

        public void ReadCrateStateFromItemStack(ItemStack stack)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.ReadCrateStateFromItemStack START | stack={stack?.Collectible?.Code}");

            if (stack?.Attributes == null || !stack.Attributes.HasAttribute(StackAttrTreeName))
            {
                DebugLogger.Log("BlockEntityResourceCrate.ReadCrateStateFromItemStack END | no crate metadata");
                return;
            }

            ITreeAttribute crateTree = stack.Attributes[StackAttrTreeName] as ITreeAttribute;
            if (crateTree == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.ReadCrateStateFromItemStack END | metadata tree missing");
                return;
            }

            state.CrateTier = crateTree.GetInt(TreeAttrCrateTier, 0);
            state.ProgressMinutes = ClampProgressMinutes(crateTree.GetDouble(TreeAttrProgressMinutes, 0));
            state.TargetItemCode = ParseAssetLocationOrNull(crateTree.GetString(TreeAttrTargetItemCode, null));
            state.LastUpdateTotalHours = 0;

            DebugLogger.Log($"BlockEntityResourceCrate.ReadCrateStateFromItemStack END | state={state}");
        }

        private void OnServerTick(float dt)
        {
            try
            {
                if (Api?.Side != EnumAppSide.Server) // conflict here
                {
                    return;
                }

                EnsureLastUpdateInitialized();

                double nowHours = Api.World.Calendar.TotalHours; // conflict here
                double elapsedHours = nowHours - state.LastUpdateTotalHours;

                // Advance the time anchor immediately so loaded-only logic stays sane.
                state.LastUpdateTotalHours = nowHours;

                if (elapsedHours <= 0)
                {
                    return;
                }

                ResourceCrateResolvedConfig config = ResolvedConfig;
                if (!ResourceCrateRules.CanGenerate(state, config))
                {
                    MarkDirty(); // conflict here
                    return;
                }

                double elapsedMinutes = ResourceCrateTierMath.HoursToMinutes(elapsedHours);
                double totalProgress = ClampProgressMinutes(state.ProgressMinutes + elapsedMinutes);

                double minutesPerItem = ResourceCrateRules.GetMinutesPerItem(state, config);
                if (minutesPerItem <= 0)
                {
                    state.ProgressMinutes = totalProgress;
                    MarkDirty(); // conflict here
                    return;
                }

                int producibleItems = (int)(totalProgress / minutesPerItem);
                int insertedItems = 0;

                if (producibleItems > 0)
                {
                    insertedItems = TryInsertProducedItems(producibleItems);
                }

                double remainingProgress = totalProgress - (insertedItems * minutesPerItem);
                state.ProgressMinutes = ClampProgressMinutes(remainingProgress);

                if (insertedItems > 0 || elapsedMinutes > 0)
                {
                    MarkDirty(); // conflict here
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"BlockEntityResourceCrate.OnServerTick | Exception: {ex}");
            }
        }

        private int TryInsertProducedItems(int count)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.TryInsertProducedItems START | count={count}, target={state.TargetItemCode}, slot={OutputSlot?.Itemstack?.Collectible?.Code}");

            if (count <= 0 || state.TargetItemCode == null || OutputSlot == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryInsertProducedItems END -> 0 (invalid preconditions)");
                return 0;
            }

            CollectibleObject collectible = Api.World.GetItem(state.TargetItemCode) ?? (CollectibleObject)Api.World.GetBlock(state.TargetItemCode); // conflict here
            if (collectible == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryInsertProducedItems END -> 0 (target collectible not found)");
                return 0;
            }

            int inserted = 0;

            if (OutputSlot.Empty)
            {
                int toCreate = Math.Min(count, collectible.MaxStackSize);
                OutputSlot.Itemstack = new ItemStack(collectible, toCreate);
                OutputSlot.MarkDirty();
                inserted += toCreate;

                DebugLogger.Log($"BlockEntityResourceCrate.TryInsertProducedItems | filled empty slot with {toCreate}");
                DebugLogger.Log($"BlockEntityResourceCrate.TryInsertProducedItems END -> {inserted}");
                return inserted;
            }

            if (OutputSlot.Itemstack?.Collectible == null || !OutputSlot.Itemstack.Collectible.Code.Equals(state.TargetItemCode))
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryInsertProducedItems END -> 0 (slot occupied by incompatible item)");
                return 0;
            }

            int current = OutputSlot.StackSize;
            int max = OutputSlot.Itemstack.Collectible.MaxStackSize;
            int room = Math.Max(0, max - current);

            if (room <= 0)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryInsertProducedItems END -> 0 (slot full)");
                return 0;
            }

            inserted = Math.Min(count, room);
            OutputSlot.Itemstack.StackSize += inserted;
            OutputSlot.MarkDirty();

            DebugLogger.Log($"BlockEntityResourceCrate.TryInsertProducedItems END -> {inserted}");
            return inserted;
        }

        private void EnsureLastUpdateInitialized()
        {
            if (Api?.World?.Calendar == null) // conflict here
            {
                return;
            }

            if (state.LastUpdateTotalHours <= 0)
            {
                state.LastUpdateTotalHours = Api.World.Calendar.TotalHours; // conflict here
            }
        }

        private static AssetLocation ParseAssetLocationOrNull(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            try
            {
                return new AssetLocation(code);
            }
            catch
            {
                return null;
            }
        }

        private static double ClampProgressMinutes(double value)
        {
            if (value < 0) return 0;
            if (value > MaxStoredProgressMinutes) return MaxStoredProgressMinutes;
            return value;
        }
    }
}