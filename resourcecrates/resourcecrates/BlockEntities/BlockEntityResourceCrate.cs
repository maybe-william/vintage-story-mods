using System;
using System.Text;
using resourcecrates.Config;
using resourcecrates.Domain;
using resourcecrates.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace resourcecrates.BlockEntities
{
    /// <summary>
    /// Plain C# controller for resource crate behavior.
    /// This is NOT the runtime-instantiated BlockEntity subclass.
    ///
    /// The real runtime/generated BE implements IResourceCrateHost and forwards
    /// lifecycle and block-facing calls into this controller.
    /// </summary>
    public class BlockEntityResourceCrate
    {
        private const string TreeAttrCrateTier = "resourcecrates:crateTier";
        private const string TreeAttrTargetItemCode = "resourcecrates:targetItemCode";
        private const string TreeAttrProgressMinutes = "resourcecrates:progressMinutes";
        private const string TreeAttrLastUpdateTotalHours = "resourcecrates:lastUpdateTotalHours";

        private const string StackAttrTreeName = "resourcecratesState";

        private const double MaxStoredProgressSeconds = 1_000_000_000d;
        private const double MaxStoredProgressMinutes = MaxStoredProgressSeconds / 60d;

        private readonly IResourceCrateHost host;
        private readonly ResourceCrateState state = new();

        private long serverTickListenerId = 0;

        /// <summary>
        /// Constructor called by the runtime-generated BE.
        /// This is the point where the dynamic BE and static controller are linked.
        /// </summary>
        public BlockEntityResourceCrate(IResourceCrateHost host)
        {
            DebugLogger.Log("BlockEntityResourceCrate.ctor START");

            this.host = host ?? throw new ArgumentNullException(nameof(host));

            DebugLogger.Log("BlockEntityResourceCrate.ctor END");
        }

        public ResourceCrateState State => state;

        private ResourceCrateResolvedConfig ResolvedConfig
            => resourcecratesModSystem.GetResolvedConfigOrThrow();

        // ---------------------------------------------------------------------
        // Lifecycle methods called by generated BE overrides
        // ---------------------------------------------------------------------

        public void Initialize(ICoreAPI api)
        {
            DebugLogger.Log("BlockEntityResourceCrate.Initialize START");

            host.CallBaseInitialize(api);

            if (api != null && api.Side == EnumAppSide.Server)
            {
                EnsureLastUpdateInitialized();

                serverTickListenerId = host.CallRegisterGameTickListener(OnServerTick, 1000);
                DebugLogger.Log($"BlockEntityResourceCrate.Initialize | Registered tick listener id={serverTickListenerId}");
            }

            DebugLogger.Log("BlockEntityResourceCrate.Initialize END");
        }

        public void OnBlockPlaced(ItemStack byItemStack = null)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.OnBlockPlaced START | byItemStack={byItemStack?.Collectible?.Code}");

            host.CallBaseOnBlockPlaced(byItemStack);

            if (byItemStack != null)
            {
                ReadCrateStateFromItemStack(byItemStack);
            }

            EnsureLastUpdateInitialized();
            host.CallMarkDirty(true);

            DebugLogger.Log($"BlockEntityResourceCrate.OnBlockPlaced END | state={state}");
        }

        public void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            DebugLogger.Log("BlockEntityResourceCrate.FromTreeAttributes START");

            host.CallBaseFromTreeAttributes(tree, worldForResolving);

            state.CrateTier = tree.GetInt(TreeAttrCrateTier, 0);
            state.ProgressMinutes = ClampProgressMinutes(tree.GetDouble(TreeAttrProgressMinutes, 0));
            state.LastUpdateTotalHours = tree.GetDouble(TreeAttrLastUpdateTotalHours, 0);

            string targetCode = tree.GetString(TreeAttrTargetItemCode, null);
            state.TargetItemCode = ParseAssetLocationOrNull(targetCode);

            DebugLogger.Log($"BlockEntityResourceCrate.FromTreeAttributes END | state={state}");
        }

        public void ToTreeAttributes(ITreeAttribute tree)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.ToTreeAttributes START | state={state}");

            host.CallBaseToTreeAttributes(tree);

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

        public void OnBlockUnloaded()
        {
            DebugLogger.Log("BlockEntityResourceCrate.OnBlockUnloaded START");

            host.CallBaseOnBlockUnloaded();

            if (serverTickListenerId != 0)
            {
                host.CallUnregisterGameTickListener(serverTickListenerId);
                serverTickListenerId = 0;
            }

            DebugLogger.Log("BlockEntityResourceCrate.OnBlockUnloaded END");
        }

        public void OnBlockRemoved()
        {
            DebugLogger.Log("BlockEntityResourceCrate.OnBlockRemoved START");

            host.CallBaseOnBlockRemoved();

            if (serverTickListenerId != 0)
            {
                host.CallUnregisterGameTickListener(serverTickListenerId);
                serverTickListenerId = 0;
            }

            DebugLogger.Log("BlockEntityResourceCrate.OnBlockRemoved END");
        }

        public void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            host.CallBaseGetBlockInfo(forPlayer, dsc);

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

        // ---------------------------------------------------------------------
        // Block-facing methods
        // These are called by BlockResourceCrate through the generated BE host.
        // ---------------------------------------------------------------------

        public bool TryUpgrade(IPlayer byPlayer, ItemSlot handSlot)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.TryUpgrade START | player={byPlayer?.PlayerName}, handSlot={handSlot?.Itemstack?.Collectible?.Code}, state={state}");

            ICoreAPI api = host.GetApi();

            if (api == null || api.Side != EnumAppSide.Server || byPlayer == null || handSlot?.Itemstack == null)
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
            host.CallMarkDirty(true);

            DebugLogger.Log($"BlockEntityResourceCrate.TryUpgrade END -> true | newState={state}");
            return true;
        }

        public bool TrySetOrReplaceTarget(IPlayer byPlayer, ItemSlot handSlot)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.TrySetOrReplaceTarget START | player={byPlayer?.PlayerName}, handSlot={handSlot?.Itemstack?.Collectible?.Code}, state={state}");

            ICoreAPI api = host.GetApi();

            if (api == null || api.Side != EnumAppSide.Server || byPlayer == null || handSlot?.Itemstack == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TrySetOrReplaceTarget END -> false (invalid preconditions)");
                return false;
            }

            ResourceCrateResolvedConfig config = ResolvedConfig;
            ItemStack heldStack = handSlot.Itemstack;
            ItemSlot outputSlot = host.GetOutputSlot();

            bool hasTarget = state.HasTargetItem;
            bool canAssign = false;
            bool canReplace = false;

            if (!hasTarget)
            {
                canAssign = ResourceCrateRules.CanAssignTarget(state, heldStack, config);
            }
            else
            {
                canReplace = ResourceCrateRules.CanReplaceTarget(state, heldStack, outputSlot?.Itemstack, config);
            }
            
            bool canChange = !hasTarget ? canAssign : canReplace;

            DebugLogger.Log(
                $"[TargetDecision] held={heldStack?.Collectible?.Code}, " +
                $"hasTarget={hasTarget}, " +
                $"output={outputSlot?.Itemstack?.Collectible?.Code}, " +
                $"canAssign={canAssign}, " +
                $"canReplace={canReplace}, " +
                $"canChange={canChange}"
            );
            
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
            host.CallMarkDirty(true);

            DebugLogger.Log($"BlockEntityResourceCrate.TrySetOrReplaceTarget END -> true | newState={state}");
            return true;
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
                if (stack.Attributes != null)
                {
                    stack.Attributes.RemoveAttribute(StackAttrTreeName);
                }

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

        // ---------------------------------------------------------------------
        // Internal crate logic
        // ---------------------------------------------------------------------

        public void SetTarget(AssetLocation targetCode)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.SetTarget START | targetCode={targetCode}, beforeState={state}");

            if (targetCode == null)
            {
                throw new ArgumentNullException(nameof(targetCode));
            }

            bool changed = state.TargetItemCode == null || !state.TargetItemCode.Equals(targetCode);

            state.TargetItemCode = targetCode.Clone();

            // v0 behavior:
            // retargeting clears saved progress, but leaves current slot contents alone.
            if (changed)
            {
                state.ProgressMinutes = 0;
            }

            DebugLogger.Log($"BlockEntityResourceCrate.SetTarget END | afterState={state}");
        }

        public void ClearOutputForUpgrade()
        {
            ItemSlot outputSlot = host.GetOutputSlot();

            DebugLogger.Log($"BlockEntityResourceCrate.ClearOutputForUpgrade START | output={outputSlot?.Itemstack?.Collectible?.Code}");

            if (outputSlot != null && !outputSlot.Empty)
            {
                outputSlot.Itemstack = null;
                outputSlot.MarkDirty();
            }

            DebugLogger.Log("BlockEntityResourceCrate.ClearOutputForUpgrade END");
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
                ICoreAPI api = host.GetApi();

                if (api?.Side != EnumAppSide.Server)
                {
                    return;
                }

                EnsureLastUpdateInitialized();

                double nowHours = api.World.Calendar.TotalHours;
                double elapsedHours = nowHours - state.LastUpdateTotalHours;

                // Loaded-only progression anchor.
                state.LastUpdateTotalHours = nowHours;

                if (elapsedHours <= 0)
                {
                    return;
                }

                ResourceCrateResolvedConfig config = ResolvedConfig;
                if (!ResourceCrateRules.CanGenerate(state, config))
                {
                    host.CallMarkDirty();
                    return;
                }

                double elapsedMinutes = ResourceCrateTierMath.HoursToMinutes(elapsedHours);
                double totalProgress = ClampProgressMinutes(state.ProgressMinutes + elapsedMinutes);

                double minutesPerItem = ResourceCrateRules.GetMinutesPerItem(state, config);
                if (minutesPerItem <= 0)
                {
                    state.ProgressMinutes = totalProgress;
                    host.CallMarkDirty();
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
                    host.CallMarkDirty();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"BlockEntityResourceCrate.OnServerTick | Exception: {ex}");
            }
        }

        private int TryInsertProducedItems(int count)
        {
            ItemSlot outputSlot = host.GetOutputSlot();

            DebugLogger.Log($"BlockEntityResourceCrate.TryInsertProducedItems START | count={count}, target={state.TargetItemCode}, slot={outputSlot?.Itemstack?.Collectible?.Code}");

            if (count <= 0 || state.TargetItemCode == null || outputSlot == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryInsertProducedItems END -> 0 (invalid preconditions)");
                return 0;
            }

            ICoreAPI api = host.GetApi();
            if (api?.World == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryInsertProducedItems END -> 0 (api/world unavailable)");
                return 0;
            }

            CollectibleObject collectible =
                api.World.GetItem(state.TargetItemCode) ??
                (CollectibleObject)api.World.GetBlock(state.TargetItemCode);

            if (collectible == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryInsertProducedItems END -> 0 (target collectible not found)");
                return 0;
            }

            int inserted = 0;

            if (outputSlot.Empty)
            {
                int toCreate = Math.Min(count, collectible.MaxStackSize);
                outputSlot.Itemstack = new ItemStack(collectible, toCreate);
                outputSlot.MarkDirty();
                inserted += toCreate;

                DebugLogger.Log($"BlockEntityResourceCrate.TryInsertProducedItems | filled empty slot with {toCreate}");
                DebugLogger.Log($"BlockEntityResourceCrate.TryInsertProducedItems END -> {inserted}");
                return inserted;
            }

            if (outputSlot.Itemstack?.Collectible == null || !outputSlot.Itemstack.Collectible.Code.Equals(state.TargetItemCode))
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryInsertProducedItems END -> 0 (slot occupied by incompatible item)");
                return 0;
            }

            int current = outputSlot.StackSize;
            int max = outputSlot.Itemstack.Collectible.MaxStackSize;
            int room = Math.Max(0, max - current);

            if (room <= 0)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryInsertProducedItems END -> 0 (slot full)");
                return 0;
            }

            inserted = Math.Min(count, room);
            outputSlot.Itemstack.StackSize += inserted;
            outputSlot.MarkDirty();

            DebugLogger.Log($"BlockEntityResourceCrate.TryInsertProducedItems END -> {inserted}");
            return inserted;
        }

        private void EnsureLastUpdateInitialized()
        {
            ICoreAPI api = host.GetApi();

            if (api?.World?.Calendar == null)
            {
                return;
            }

            if (state.LastUpdateTotalHours <= 0)
            {
                state.LastUpdateTotalHours = api.World.Calendar.TotalHours;
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