using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using resourcecrates.Config;
using resourcecrates.Domain;
using resourcecrates.Inventory;
using resourcecrates.Serialization;
using resourcecrates.Util;

namespace resourcecrates.BlockEntities
{
    public class BlockEntityResourceCrate : BlockEntity, IBlockEntityContainer
    {
        public const string TreeStateKey = "resourceCrateState";
        public const int TickIntervalMs = 1000;

        private InventoryResourceCrate inventory;
        private ResourceCrateState state;
        private long tickListenerId = -1;

        public BlockEntityResourceCrate()
        {
            DebugLogger.Log("BlockEntityResourceCrate.ctor START");

            inventory = null;
            state = new ResourceCrateState();

            DebugLogger.Log("BlockEntityResourceCrate.ctor END");
        }

        public InventoryBase Inventory
        {
            get
            {
                DebugLogger.Log("BlockEntityResourceCrate.Inventory START");

                InventoryBase result = inventory;

                DebugLogger.Log("BlockEntityResourceCrate.Inventory END");
                return result;
            }
        }

        public string InventoryClassName
        {
            get
            {
                DebugLogger.Log("BlockEntityResourceCrate.InventoryClassName START");

                string result = InventoryResourceCrate.InventoryClassNameValue;

                DebugLogger.Log($"BlockEntityResourceCrate.InventoryClassName END -> {result}");
                return result;
            }
        }

        public ResourceCrateState State
        {
            get
            {
                DebugLogger.Log("BlockEntityResourceCrate.State START");

                ResourceCrateState result = state;

                DebugLogger.Log("BlockEntityResourceCrate.State END");
                return result;
            }
        }

        public InventoryResourceCrate ResourceInventory
        {
            get
            {
                DebugLogger.Log("BlockEntityResourceCrate.ResourceInventory START");

                InventoryResourceCrate result = inventory;

                DebugLogger.Log("BlockEntityResourceCrate.ResourceInventory END");
                return result;
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            DebugLogger.Log("BlockEntityResourceCrate.Initialize START");

            base.Initialize(api);

            EnsureInventoryInitialized(api);

            if (api?.Side == EnumAppSide.Server)
            {
                state.LastUpdateTotalHours = GetCurrentTotalHours();
                tickListenerId = RegisterGameTickListener(OnServerTick, TickIntervalMs);
                DebugLogger.Log($"BlockEntityResourceCrate.Initialize | Registered server tick listener id={tickListenerId}");
            }

            MarkDirty();

            DebugLogger.Log("BlockEntityResourceCrate.Initialize END");
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.OnBlockPlaced START | byItemStackNull={byItemStack == null}");

            base.OnBlockPlaced(byItemStack);

            if (byItemStack != null && ResourceCrateStackAttributes.TryReadFromStack(byItemStack, out ResourceCrateState restoredState))
            {
                state = restoredState;
                DebugLogger.Log($"BlockEntityResourceCrate.OnBlockPlaced | Restored state from itemstack: {state}");
            }
            else
            {
                state = new ResourceCrateState();
                DebugLogger.Log("BlockEntityResourceCrate.OnBlockPlaced | No metadata found, using fresh default state");
            }

            state.LastUpdateTotalHours = GetCurrentTotalHours();
            MarkDirty();

            DebugLogger.Log("BlockEntityResourceCrate.OnBlockPlaced END");
        }

        public override void OnBlockRemoved()
        {
            DebugLogger.Log("BlockEntityResourceCrate.OnBlockRemoved START");

            if (Api?.Side == EnumAppSide.Server && tickListenerId >= 0)
            {
                UnregisterGameTickListener(tickListenerId);
                DebugLogger.Log($"BlockEntityResourceCrate.OnBlockRemoved | Unregistered tick listener id={tickListenerId}");
                tickListenerId = -1;
            }

            base.OnBlockRemoved();

            DebugLogger.Log("BlockEntityResourceCrate.OnBlockRemoved END");
        }

        public override void OnBlockUnloaded()
        {
            DebugLogger.Log("BlockEntityResourceCrate.OnBlockUnloaded START");

            if (Api?.Side == EnumAppSide.Server && tickListenerId >= 0)
            {
                UnregisterGameTickListener(tickListenerId);
                DebugLogger.Log($"BlockEntityResourceCrate.OnBlockUnloaded | Unregistered tick listener id={tickListenerId}");
                tickListenerId = -1;
            }

            base.OnBlockUnloaded();

            DebugLogger.Log("BlockEntityResourceCrate.OnBlockUnloaded END");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            DebugLogger.Log("BlockEntityResourceCrate.ToTreeAttributes START");

            base.ToTreeAttributes(tree);

            EnsureInventoryInitialized(Api);
            inventory?.ToTreeAttributes(tree);

            TreeAttribute stateTree = new TreeAttribute();
            stateTree.SetInt(ResourceCrateStackAttributes.CrateTierKey, state.CrateTier);
            stateTree.SetDouble(ResourceCrateStackAttributes.ProgressMinutesKey, state.ProgressMinutes);
            stateTree.SetDouble(ResourceCrateStackAttributes.LastUpdateTotalHoursKey, state.LastUpdateTotalHours);

            if (state.TargetItemCode != null)
            {
                stateTree.SetString(ResourceCrateStackAttributes.TargetItemCodeKey, state.TargetItemCode.ToShortString());
            }

            tree[TreeStateKey] = stateTree;

            DebugLogger.Log("BlockEntityResourceCrate.ToTreeAttributes END");
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            DebugLogger.Log("BlockEntityResourceCrate.FromTreeAttributes START");

            base.FromTreeAttributes(tree, worldForResolving);

            EnsureInventoryInitialized(Api);

            inventory?.FromTreeAttributes(tree);

            ITreeAttribute stateTree = tree?[TreeStateKey] as ITreeAttribute;
            if (stateTree != null)
            {
                state.CrateTier = stateTree.GetInt(ResourceCrateStackAttributes.CrateTierKey);
                state.ProgressMinutes = stateTree.GetDouble(ResourceCrateStackAttributes.ProgressMinutesKey);
                state.LastUpdateTotalHours = stateTree.GetDouble(ResourceCrateStackAttributes.LastUpdateTotalHoursKey);

                string targetCode = stateTree.GetString(ResourceCrateStackAttributes.TargetItemCodeKey, null);
                state.TargetItemCode = string.IsNullOrWhiteSpace(targetCode) ? null : new AssetLocation(targetCode);

                DebugLogger.Log($"BlockEntityResourceCrate.FromTreeAttributes | Restored state from tree: {state}");
            }
            else
            {
                state = new ResourceCrateState();
                DebugLogger.Log("BlockEntityResourceCrate.FromTreeAttributes | No state tree found, reset to default state");
            }

            DebugLogger.Log("BlockEntityResourceCrate.FromTreeAttributes END");
        }

        public bool TryUpgrade(ItemSlot heldSlot)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.TryUpgrade START | heldSlotNull={heldSlot == null}");

            bool result = false;

            if (heldSlot?.Itemstack == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryUpgrade END -> false (heldSlot or stack null)");
                return false;
            }

            ResourceCrateResolvedConfig config = ResourceCratesModSystem.GetResolvedConfigOrThrow();

            if (!ResourceCrateRules.CanUpgrade(state, heldSlot.Itemstack, config))
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryUpgrade END -> false (rules rejected)");
                return false;
            }

            int targetTier = ResourceCrateRules.GetUpgradeTargetTier(state, heldSlot.Itemstack, config);
            if (targetTier <= state.CrateTier)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryUpgrade END -> false (target tier not higher)");
                return false;
            }

            state.CrateTier = targetTier;

            if (ResourceCrateRules.ShouldConsumeUpgradeItem(state, heldSlot.Itemstack, config))
            {
                heldSlot.TakeOut(1);
                heldSlot.MarkDirty();
            }

            MarkDirty();
            result = true;

            DebugLogger.Log($"BlockEntityResourceCrate.TryUpgrade END -> {result}");
            return result;
        }

        public bool TryAssignTarget(ItemSlot heldSlot)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.TryAssignTarget START | heldSlotNull={heldSlot == null}");

            bool result = false;

            if (heldSlot?.Itemstack == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryAssignTarget END -> false (heldSlot or stack null)");
                return false;
            }

            ResourceCrateResolvedConfig config = ResourceCratesModSystem.GetResolvedConfigOrThrow();

            if (!ResourceCrateRules.CanAssignTarget(state, heldSlot.Itemstack, config))
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryAssignTarget END -> false (rules rejected)");
                return false;
            }

            state.TargetItemCode = heldSlot.Itemstack.Collectible.Code;
            state.ProgressMinutes = 0;
            state.LastUpdateTotalHours = GetCurrentTotalHours();

            if (ResourceCrateRules.ShouldConsumeTargetItem(state, heldSlot.Itemstack, config))
            {
                heldSlot.TakeOut(1);
                heldSlot.MarkDirty();
            }

            MarkDirty();
            result = true;

            DebugLogger.Log($"BlockEntityResourceCrate.TryAssignTarget END -> {result}");
            return result;
        }

        public bool TryReplaceTarget(ItemSlot heldSlot)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.TryReplaceTarget START | heldSlotNull={heldSlot == null}");

            bool result = false;

            if (heldSlot?.Itemstack == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryReplaceTarget END -> false (heldSlot or stack null)");
                return false;
            }

            ItemStack currentStoredStack = inventory?.OutputSlot?.Itemstack;
            ResourceCrateResolvedConfig config = ResourceCratesModSystem.GetResolvedConfigOrThrow();

            if (!ResourceCrateRules.CanReplaceTarget(state, heldSlot.Itemstack, currentStoredStack, config))
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryReplaceTarget END -> false (rules rejected)");
                return false;
            }

            state.TargetItemCode = heldSlot.Itemstack.Collectible.Code;
            state.ProgressMinutes = 0;
            state.LastUpdateTotalHours = GetCurrentTotalHours();

            if (ResourceCrateRules.ShouldConsumeTargetItem(state, heldSlot.Itemstack, config))
            {
                heldSlot.TakeOut(1);
                heldSlot.MarkDirty();
            }

            MarkDirty();
            result = true;

            DebugLogger.Log($"BlockEntityResourceCrate.TryReplaceTarget END -> {result}");
            return result;
        }

        public bool CanPlayerTake()
        {
            DebugLogger.Log("BlockEntityResourceCrate.CanPlayerTake START");

            bool result = inventory != null && inventory.OutputSlot != null;

            DebugLogger.Log($"BlockEntityResourceCrate.CanPlayerTake END -> {result}");
            return result;
        }

        public ItemStack CreateDroppedStack()
        {
            DebugLogger.Log("BlockEntityResourceCrate.CreateDroppedStack START");

            ItemStack result = new ItemStack(Block);

            if (ResourceCrateRules.ShouldPreserveMetadata(state))
            {
                ResourceCrateStackAttributes.WriteToStack(result, state);
            }
            else
            {
                ResourceCrateStackAttributes.ClearStackData(result);
            }

            DebugLogger.Log("BlockEntityResourceCrate.CreateDroppedStack END");
            return result;
        }

        public void ClearTarget()
        {
            DebugLogger.Log("BlockEntityResourceCrate.ClearTarget START");

            state.ClearTarget();
            state.LastUpdateTotalHours = GetCurrentTotalHours();
            MarkDirty();

            DebugLogger.Log("BlockEntityResourceCrate.ClearTarget END");
        }

        private void OnServerTick(float dt)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.OnServerTick START | dt={dt}");

            if (Api == null || Api.Side != EnumAppSide.Server)
            {
                DebugLogger.Log("BlockEntityResourceCrate.OnServerTick END (not server or Api null)");
                return;
            }

            EnsureInventoryInitialized(Api);

            ResourceCrateResolvedConfig config = ResourceCratesModSystem.GetResolvedConfigOrThrow();

            if (!ResourceCrateRules.CanGenerate(state, config))
            {
                state.LastUpdateTotalHours = GetCurrentTotalHours();
                DebugLogger.Log("BlockEntityResourceCrate.OnServerTick END (cannot generate)");
                return;
            }

            CollectibleObject collectible = ResolveTargetCollectible();
            if (collectible == null)
            {
                state.LastUpdateTotalHours = GetCurrentTotalHours();
                DebugLogger.Warn("BlockEntityResourceCrate.OnServerTick END (target collectible unresolved)");
                return;
            }

            ItemStack probeStack = new ItemStack(collectible, 1);
            if (!inventory.OutputSlot.CanAcceptGenerated(probeStack))
            {
                state.LastUpdateTotalHours = GetCurrentTotalHours();
                DebugLogger.Log("BlockEntityResourceCrate.OnServerTick END (slot cannot accept generated item)");
                return;
            }

            double currentTotalHours = GetCurrentTotalHours();
            double elapsedHours = currentTotalHours - state.LastUpdateTotalHours;
            if (elapsedHours < 0)
            {
                elapsedHours = 0;
            }

            double elapsedMinutes = ResourceCrateTierMath.HoursToMinutes(elapsedHours);
            double minutesPerItem = ResourceCrateRules.GetMinutesPerItem(state, config);

            if (minutesPerItem <= 0)
            {
                state.LastUpdateTotalHours = currentTotalHours;
                DebugLogger.Log("BlockEntityResourceCrate.OnServerTick END (minutesPerItem invalid)");
                return;
            }

            var production = ResourceCrateTierMath.ComputeProduction(state.ProgressMinutes, minutesPerItem, elapsedMinutes);
            int itemsToProduce = production.items;
            double remainingProgress = production.remainingProgress;

            if (itemsToProduce <= 0)
            {
                state.ProgressMinutes = remainingProgress;
                state.LastUpdateTotalHours = currentTotalHours;
                MarkDirty();
                DebugLogger.Log("BlockEntityResourceCrate.OnServerTick END (not enough progress for item)");
                return;
            }

            int remainingRoom = inventory.OutputSlot.GetRemainingRoomFor(probeStack);
            if (remainingRoom <= 0)
            {
                state.LastUpdateTotalHours = currentTotalHours;
                DebugLogger.Log("BlockEntityResourceCrate.OnServerTick END (no room after room check)");
                return;
            }

            int actualToProduce = itemsToProduce <= remainingRoom ? itemsToProduce : remainingRoom;

            ItemStack generatedStack = new ItemStack(collectible, actualToProduce);
            int inserted = inventory.OutputSlot.TryPutGenerated(generatedStack);

            int uninserted = itemsToProduce - inserted;
            state.ProgressMinutes = remainingProgress + (uninserted * minutesPerItem);
            state.LastUpdateTotalHours = currentTotalHours;

            if (inserted > 0)
            {
                MarkDirty();
            }

            DebugLogger.Log($"BlockEntityResourceCrate.OnServerTick END | inserted={inserted}, uninserted={uninserted}, state={state}");
        }

        private void EnsureInventoryInitialized(ICoreAPI api)
        {
            DebugLogger.Log("BlockEntityResourceCrate.EnsureInventoryInitialized START");

            if (inventory == null)
            {
                string inventoryId = $"resourcecrate-{Pos?.X ?? 0}/{Pos?.Y ?? 0}/{Pos?.Z ?? 0}";
                inventory = new InventoryResourceCrate(inventoryId, api);
                DebugLogger.Log($"BlockEntityResourceCrate.EnsureInventoryInitialized | Created inventory id={inventoryId}");
            }

            DebugLogger.Log("BlockEntityResourceCrate.EnsureInventoryInitialized END");
        }

        private double GetCurrentTotalHours()
        {
            DebugLogger.Log("BlockEntityResourceCrate.GetCurrentTotalHours START");

            double result = 0;

            if (Api?.World?.Calendar != null)
            {
                result = Api.World.Calendar.TotalHours;
            }

            DebugLogger.Log($"BlockEntityResourceCrate.GetCurrentTotalHours END -> {result}");
            return result;
        }

        private CollectibleObject ResolveTargetCollectible()
        {
            DebugLogger.Log("BlockEntityResourceCrate.ResolveTargetCollectible START");

            CollectibleObject result = null;

            if (Api?.World == null || state?.TargetItemCode == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.ResolveTargetCollectible END -> null (Api/world/target missing)");
                return null;
            }

            result = Api.World.GetItem(state.TargetItemCode);
            if (result == null)
            {
                result = Api.World.GetBlock(state.TargetItemCode);
            }

            DebugLogger.Log($"BlockEntityResourceCrate.ResolveTargetCollectible END -> {(result == null ? "null" : result.Code.ToString())}");
            return result;
        }
    }
}