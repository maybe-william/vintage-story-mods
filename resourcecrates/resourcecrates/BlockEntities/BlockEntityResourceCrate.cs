using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using resourcecrates.Config;
using resourcecrates.Domain;
using resourcecrates.Inventory;
using resourcecrates.Serialization;
using resourcecrates.Util;
using Vintagestory.API.Client;
using resourcecrates.Gui;

namespace resourcecrates.BlockEntities
{
    public class BlockEntityResourceCrate : BlockEntity, IBlockEntityContainer
    {
        private const string TreeStateKey = "resourceCrateState";
        private const int TickIntervalMs = 1000;

        private InventoryResourceCrate? _inventory;
        private ResourceCrateState _state;
        private long _tickListenerId = -1;
        private GuiDialogResourceCrate? _clientDialog;

        public BlockEntityResourceCrate()
        {
            DebugLogger.Log("BlockEntityResourceCrate.ctor START");

            _state = new ResourceCrateState();

            DebugLogger.Log("BlockEntityResourceCrate.ctor END");
        }

        public IInventory Inventory
        {
            get
            {
                DebugLogger.Log("BlockEntityResourceCrate.Inventory START");

                var result = InventoryOrThrow();

                DebugLogger.Log("BlockEntityResourceCrate.Inventory END");
                return result;
            }
        }

        public string InventoryClassName
        {
            get
            {
                DebugLogger.Log("BlockEntityResourceCrate.InventoryClassName START");

                var result = InventoryResourceCrate.InventoryClassNameValue;

                DebugLogger.Log($"BlockEntityResourceCrate.InventoryClassName END -> {result}");
                return result;
            }
        }

        public ResourceCrateState State
        {
            get
            {
                DebugLogger.Log("BlockEntityResourceCrate.State START");

                var result = _state;

                DebugLogger.Log("BlockEntityResourceCrate.State END");
                return result;
            }
        }

        public InventoryResourceCrate ResourceInventory
        {
            get
            {
                DebugLogger.Log("BlockEntityResourceCrate.ResourceInventory START");

                var result = InventoryOrThrow();

                DebugLogger.Log("BlockEntityResourceCrate.ResourceInventory END");
                return result;
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            DebugLogger.Log("BlockEntityResourceCrate.Initialize START");

            base.Initialize(api);

            EnsureInventoryInitialized(api);

            if (api.Side == EnumAppSide.Server)
            {
                _state.LastUpdateTotalHours = GetCurrentTotalHours();
                _tickListenerId = RegisterGameTickListener(OnServerTick, TickIntervalMs);
                DebugLogger.Log($"BlockEntityResourceCrate.Initialize | Registered server tick listener id={_tickListenerId}");
            }

            MarkDirty();

            DebugLogger.Log("BlockEntityResourceCrate.Initialize END");
        }

        public override void OnBlockPlaced(ItemStack? byItemStack)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.OnBlockPlaced START | byItemStackNull={byItemStack == null}");

            base.OnBlockPlaced(byItemStack);

            if (byItemStack != null && ResourceCrateStackAttributes.TryReadFromStack(byItemStack, out var restoredState))
            {
                _state = restoredState;
                DebugLogger.Log($"BlockEntityResourceCrate.OnBlockPlaced | Restored state from itemstack: {_state}");
            }
            else
            {
                _state = new ResourceCrateState();
                DebugLogger.Log("BlockEntityResourceCrate.OnBlockPlaced | No metadata found, using fresh default state");
            }

            _state.LastUpdateTotalHours = GetCurrentTotalHours();
            MarkDirty();

            DebugLogger.Log("BlockEntityResourceCrate.OnBlockPlaced END");
        }

        public override void OnBlockRemoved()
        {
            DebugLogger.Log("BlockEntityResourceCrate.OnBlockRemoved START");

            TryCloseDialog();

            if (Api?.Side == EnumAppSide.Server && _tickListenerId >= 0)
            {
                UnregisterGameTickListener(_tickListenerId);
                DebugLogger.Log($"BlockEntityResourceCrate.OnBlockRemoved | Unregistered tick listener id={_tickListenerId}");
                _tickListenerId = -1;
            }

            base.OnBlockRemoved();

            DebugLogger.Log("BlockEntityResourceCrate.OnBlockRemoved END");
        }

        public override void OnBlockUnloaded()
        {
            DebugLogger.Log("BlockEntityResourceCrate.OnBlockUnloaded START");

            TryCloseDialog();

            if (Api?.Side == EnumAppSide.Server && _tickListenerId >= 0)
            {
                UnregisterGameTickListener(_tickListenerId);
                DebugLogger.Log($"BlockEntityResourceCrate.OnBlockUnloaded | Unregistered tick listener id={_tickListenerId}");
                _tickListenerId = -1;
            }

            base.OnBlockUnloaded();

            DebugLogger.Log("BlockEntityResourceCrate.OnBlockUnloaded END");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            DebugLogger.Log("BlockEntityResourceCrate.ToTreeAttributes START");

            base.ToTreeAttributes(tree);

            var inventory = InventoryOrThrow();
            inventory.ToTreeAttributes(tree);

            var stateTree = new TreeAttribute();
            stateTree.SetInt(ResourceCrateStackAttributes.CrateTierKey, _state.CrateTier);
            stateTree.SetDouble(ResourceCrateStackAttributes.ProgressMinutesKey, _state.ProgressMinutes);
            stateTree.SetDouble(ResourceCrateStackAttributes.LastUpdateTotalHoursKey, _state.LastUpdateTotalHours);

            if (_state.TargetItemCode != null)
            {
                stateTree.SetString(ResourceCrateStackAttributes.TargetItemCodeKey, _state.TargetItemCode.ToShortString());
            }

            tree[TreeStateKey] = stateTree;

            DebugLogger.Log("BlockEntityResourceCrate.ToTreeAttributes END");
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            DebugLogger.Log("BlockEntityResourceCrate.FromTreeAttributes START");

            base.FromTreeAttributes(tree, worldAccessForResolve);

            EnsureInventoryInitialized(worldAccessForResolve?.Api);

            if (_inventory != null)
            {
                _inventory.FromTreeAttributes(tree);
            }

            var stateTree = tree?[TreeStateKey] as ITreeAttribute;
            if (stateTree != null)
            {
                _state.CrateTier = stateTree.GetInt(ResourceCrateStackAttributes.CrateTierKey);
                _state.ProgressMinutes = stateTree.GetDouble(ResourceCrateStackAttributes.ProgressMinutesKey);
                _state.LastUpdateTotalHours = stateTree.GetDouble(ResourceCrateStackAttributes.LastUpdateTotalHoursKey);

                string targetCode = stateTree.GetString(ResourceCrateStackAttributes.TargetItemCodeKey, "");
                _state.TargetItemCode = string.IsNullOrWhiteSpace(targetCode) ? null : new AssetLocation(targetCode);

                DebugLogger.Log($"BlockEntityResourceCrate.FromTreeAttributes | Restored state from tree: {_state}");
            }
            else
            {
                _state = new ResourceCrateState();
                DebugLogger.Log("BlockEntityResourceCrate.FromTreeAttributes | No state tree found, reset to default state");
            }

            DebugLogger.Log("BlockEntityResourceCrate.FromTreeAttributes END");
        }

        public void DropContents(Vec3d atPos)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.DropContents START | atPos={atPos}");

            var inventory = InventoryOrThrow();
            if (inventory.OutputSlot.Itemstack != null && inventory.OutputSlot.Itemstack.StackSize > 0)
            {
                var stack = inventory.OutputSlot.Itemstack.Clone();
                Api.World.SpawnItemEntity(stack, atPos);
                inventory.OutputSlot.Itemstack = null;
                inventory.OutputSlot.MarkDirty();
            }

            DebugLogger.Log("BlockEntityResourceCrate.DropContents END");
        }

        public void CheckInventoryClearedMidTick()
        {
            DebugLogger.Log("BlockEntityResourceCrate.CheckInventoryClearedMidTick START");
            DebugLogger.Log("BlockEntityResourceCrate.CheckInventoryClearedMidTick END");
        }

        public bool TryUpgrade(ItemSlot heldSlot)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.TryUpgrade START | heldSlotNull={heldSlot == null}");

            if (heldSlot?.Itemstack == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryUpgrade END -> false (heldSlot or stack null)");
                return false;
            }

            ResourceCrateResolvedConfig config = resourcecratesModSystem.GetResolvedConfigOrThrow();

            int oldTier = _state.CrateTier;
            int targetTier = ResourceCrateRules.GetUpgradeTargetTier(_state, heldSlot.Itemstack, config);

            if (targetTier <= oldTier)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryUpgrade END -> false (target tier not higher)");
                return false;
            }

            bool shouldConsume = ResourceCrateRules.ShouldConsumeUpgradeItem(_state, heldSlot.Itemstack, config);

            _state.CrateTier = targetTier;

            if (shouldConsume)
            {
                heldSlot.TakeOut(1);
                heldSlot.MarkDirty();
            }

            MarkDirty();

            DebugLogger.Log("BlockEntityResourceCrate.TryUpgrade END -> true");
            return true;
        }

        public bool TryAssignTarget(ItemSlot heldSlot)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.TryAssignTarget START | heldSlotNull={heldSlot == null}");

            if (heldSlot?.Itemstack == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryAssignTarget END -> false (heldSlot or stack null)");
                return false;
            }

            ResourceCrateResolvedConfig config = resourcecratesModSystem.GetResolvedConfigOrThrow();

            if (!ResourceCrateRules.CanAssignTarget(_state, heldSlot.Itemstack, config))
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryAssignTarget END -> false (rules rejected)");
                return false;
            }

            _state.TargetItemCode = heldSlot.Itemstack.Collectible.Code;
            _state.ProgressMinutes = 0;
            _state.LastUpdateTotalHours = GetCurrentTotalHours();

            if (ResourceCrateRules.ShouldConsumeTargetItem(_state, heldSlot.Itemstack, config))
            {
                heldSlot.TakeOut(1);
                heldSlot.MarkDirty();
            }

            MarkDirty();

            DebugLogger.Log("BlockEntityResourceCrate.TryAssignTarget END -> true");
            return true;
        }

        public bool TryReplaceTarget(ItemSlot heldSlot)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.TryReplaceTarget START | heldSlotNull={heldSlot == null}");

            if (heldSlot?.Itemstack == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryReplaceTarget END -> false (heldSlot or stack null)");
                return false;
            }

            var inventory = InventoryOrThrow();
            var currentStoredStack = inventory.OutputSlot.Itemstack;
            ResourceCrateResolvedConfig config = resourcecratesModSystem.GetResolvedConfigOrThrow();

            if (!ResourceCrateRules.CanReplaceTarget(_state, heldSlot.Itemstack, currentStoredStack, config))
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryReplaceTarget END -> false (rules rejected)");
                return false;
            }

            if (currentStoredStack != null && currentStoredStack.StackSize > 0)
            {
                var dropPos = Pos.ToVec3d().Add(0.5, 0.5, 0.5);
                var droppedStack = currentStoredStack.Clone();

                Api.World.SpawnItemEntity(droppedStack, dropPos);

                inventory.OutputSlot.Itemstack = null;
                inventory.OutputSlot.MarkDirty();

                DebugLogger.Log($"BlockEntityResourceCrate.TryReplaceTarget | Dropped previous contents: {droppedStack.Collectible.Code} x{droppedStack.StackSize}");
            }

            _state.TargetItemCode = heldSlot.Itemstack.Collectible.Code;
            _state.ProgressMinutes = 0;
            _state.LastUpdateTotalHours = GetCurrentTotalHours();

            if (ResourceCrateRules.ShouldConsumeTargetItem(_state, heldSlot.Itemstack, config))
            {
                heldSlot.TakeOut(1);
                heldSlot.MarkDirty();
            }

            MarkDirty();

            DebugLogger.Log("BlockEntityResourceCrate.TryReplaceTarget END -> true");
            return true;
        }

        public ItemStack CreateDroppedStack()
        {
            DebugLogger.Log("BlockEntityResourceCrate.CreateDroppedStack START");

            var result = new ItemStack(Block);

            if (ResourceCrateRules.ShouldPreserveMetadata(_state))
            {
                ResourceCrateStackAttributes.WriteToStack(result, _state);
            }
            else
            {
                ResourceCrateStackAttributes.ClearStackData(result);
            }

            DebugLogger.Log("BlockEntityResourceCrate.CreateDroppedStack END");
            return result;
        }

        private void OnServerTick(float dt)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.OnServerTick START | dt={dt}");

            if (Api == null || Api.Side != EnumAppSide.Server)
            {
                DebugLogger.Log("BlockEntityResourceCrate.OnServerTick END (not server or Api null)");
                return;
            }

            var inventory = InventoryOrThrow();
            ResourceCrateResolvedConfig config = resourcecratesModSystem.GetResolvedConfigOrThrow();

            if (!ResourceCrateRules.CanGenerate(_state, config))
            {
                _state.LastUpdateTotalHours = GetCurrentTotalHours();
                DebugLogger.Log("BlockEntityResourceCrate.OnServerTick END (cannot generate)");
                return;
            }

            var collectible = ResolveTargetCollectible();
            if (collectible == null)
            {
                _state.LastUpdateTotalHours = GetCurrentTotalHours();
                DebugLogger.Warn("BlockEntityResourceCrate.OnServerTick END (target collectible unresolved)");
                return;
            }

            var probeStack = new ItemStack(collectible, 1);
            if (!inventory.OutputSlot.CanAcceptGenerated(probeStack))
            {
                _state.LastUpdateTotalHours = GetCurrentTotalHours();
                DebugLogger.Log("BlockEntityResourceCrate.OnServerTick END (slot cannot accept generated item)");
                return;
            }

            double currentTotalHours = GetCurrentTotalHours();
            double elapsedHours = currentTotalHours - _state.LastUpdateTotalHours;
            if (elapsedHours < 0) elapsedHours = 0;

            double elapsedMinutes = ResourceCrateTierMath.HoursToMinutes(elapsedHours);
            double minutesPerItem = ResourceCrateRules.GetMinutesPerItem(_state, config);

            if (minutesPerItem <= 0)
            {
                _state.LastUpdateTotalHours = currentTotalHours;
                DebugLogger.Log("BlockEntityResourceCrate.OnServerTick END (minutesPerItem invalid)");
                return;
            }

            var (itemsToProduce, remainingProgress) =
                ResourceCrateTierMath.ComputeProduction(_state.ProgressMinutes, minutesPerItem, elapsedMinutes);

            if (itemsToProduce <= 0)
            {
                _state.ProgressMinutes = remainingProgress;
                _state.LastUpdateTotalHours = currentTotalHours;
                MarkDirty();
                DebugLogger.Log("BlockEntityResourceCrate.OnServerTick END (not enough progress for item)");
                return;
            }

            int remainingRoom = inventory.OutputSlot.GetRemainingRoomFor(probeStack);
            if (remainingRoom <= 0)
            {
                _state.LastUpdateTotalHours = currentTotalHours;
                DebugLogger.Log("BlockEntityResourceCrate.OnServerTick END (no room after room check)");
                return;
            }

            int actualToProduce = itemsToProduce <= remainingRoom ? itemsToProduce : remainingRoom;

            var generatedStack = new ItemStack(collectible, actualToProduce);
            int inserted = inventory.OutputSlot.TryPutGenerated(generatedStack);

            int uninserted = itemsToProduce - inserted;
            _state.ProgressMinutes = remainingProgress + (uninserted * minutesPerItem);
            _state.LastUpdateTotalHours = currentTotalHours;

            if (inserted > 0)
            {
                MarkDirty();
            }

            DebugLogger.Log($"BlockEntityResourceCrate.OnServerTick END | inserted={inserted}, uninserted={uninserted}, state={_state}");
        }

        private void EnsureInventoryInitialized(ICoreAPI? api)
        {
            DebugLogger.Log("BlockEntityResourceCrate.EnsureInventoryInitialized START");

            if (_inventory == null)
            {
                if (api == null)
                {
                    DebugLogger.Log("BlockEntityResourceCrate.EnsureInventoryInitialized END (api null, inventory not created)");
                    return;
                }

                string inventoryId = $"resourcecrate-{Pos.X}/{Pos.Y}/{Pos.Z}";
                _inventory = new InventoryResourceCrate(inventoryId, api);
                DebugLogger.Log($"BlockEntityResourceCrate.EnsureInventoryInitialized | Created inventory id={inventoryId}");
            }

            DebugLogger.Log("BlockEntityResourceCrate.EnsureInventoryInitialized END");
        }

        private InventoryResourceCrate InventoryOrThrow()
        {
            DebugLogger.Log("BlockEntityResourceCrate.InventoryOrThrow START");

            if (_inventory == null)
            {
                DebugLogger.Error("BlockEntityResourceCrate.InventoryOrThrow | _inventory was null");
                throw new InvalidOperationException("Inventory was not initialized");
            }

            DebugLogger.Log("BlockEntityResourceCrate.InventoryOrThrow END");
            return _inventory;
        }

        private double GetCurrentTotalHours()
        {
            DebugLogger.Log("BlockEntityResourceCrate.GetCurrentTotalHours START");

            double result = Api?.World?.Calendar?.TotalHours ?? 0;

            DebugLogger.Log($"BlockEntityResourceCrate.GetCurrentTotalHours END -> {result}");
            return result;
        }

        private CollectibleObject? ResolveTargetCollectible()
        {
            DebugLogger.Log("BlockEntityResourceCrate.ResolveTargetCollectible START");

            if (Api?.World == null || _state.TargetItemCode == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.ResolveTargetCollectible END -> null (Api/world/target missing)");
                return null;
            }

            var result = (CollectibleObject?)Api.World.GetItem(_state.TargetItemCode)
                         ?? Api.World.GetBlock(_state.TargetItemCode);
            
            DebugLogger.Log($"BlockEntityResourceCrate.ResolveTargetCollectible END -> {(result == null ? "null" : result.Code.ToString())}");
            return result;
        }
        
        public bool TryOpenDialog(IPlayer byPlayer)
        {
            DebugLogger.Log($"BlockEntityResourceCrate.TryOpenDialog START | byPlayerNull={byPlayer == null}");

            if (byPlayer == null)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryOpenDialog END -> false (player null)");
                return false;
            }

            if (Api == null || Api.Side != EnumAppSide.Client)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryOpenDialog END -> false (not client side)");
                return false;
            }

            if (Api is not ICoreClientAPI capi)
            {
                DebugLogger.Log("BlockEntityResourceCrate.TryOpenDialog END -> false (Api not client api)");
                return false;
            }

            var inventory = InventoryOrThrow();

            if (_clientDialog == null)
            {
                _clientDialog = new GuiDialogResourceCrate("Resource Crate", inventory, Pos, capi);
            }

            if (!_clientDialog.IsOpened())
            {
                _clientDialog.TryOpen();
            }

            DebugLogger.Log("BlockEntityResourceCrate.TryOpenDialog END -> true");
            return true;
        }

        public void TryCloseDialog()
        {
            DebugLogger.Log("BlockEntityResourceCrate.TryCloseDialog START");

            if (_clientDialog != null && _clientDialog.IsOpened())
            {
                _clientDialog.TryClose();
            }

            if (Api is ICoreClientAPI capi)
            {
                capi.World.Player?.InventoryManager?.CloseInventory(ResourceInventory);
            }

            DebugLogger.Log("BlockEntityResourceCrate.TryCloseDialog END");
        }
    }
}