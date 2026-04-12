using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using resourcecrates.BlockEntities;
using resourcecrates.Util;

namespace resourcecrates.Blocks
{
    public class BlockResourceCrate : Block
    {
        public BlockResourceCrate()
        {
            DebugLogger.Log("BlockResourceCrate.ctor START");

            DebugLogger.Log("BlockResourceCrate.ctor END");
        }

        public override bool OnBlockInteractStart(
            IWorldAccessor world,
            IPlayer byPlayer,
            BlockSelection blockSel)
        {
            DebugLogger.Log($"BlockResourceCrate.OnBlockInteractStart START | byPlayerNull={byPlayer == null}, blockSelNull={blockSel == null}");

            bool result = false;

            if (world == null || byPlayer == null || blockSel == null)
            {
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> false (null argument)");
                return false;
            }

            BlockEntityResourceCrate be = GetBlockEntity(world, blockSel.Position);
            if (be == null)
            {
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> false (no block entity)");
                return false;
            }

            ItemSlot activeHotbarSlot = byPlayer.InventoryManager?.ActiveHotbarSlot;

            if (TryHandleUpgrade(byPlayer, activeHotbarSlot, be))
            {
                result = true;
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> true (upgrade handled)");
                return true;
            }

            if (TryHandleAssignTarget(byPlayer, activeHotbarSlot, be))
            {
                result = true;
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> true (assign target handled)");
                return true;
            }

            if (TryHandleReplaceTarget(byPlayer, activeHotbarSlot, be))
            {
                result = true;
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> true (replace target handled)");
                return true;
            }

            if (activeHotbarSlot?.Itemstack == null)
            {
                if (TryHandleOpenDialog(byPlayer, be))
                {
                    DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> true (dialog opened)");
                    return true;
                }
            }
            
            result = base.OnBlockInteractStart(world, byPlayer, blockSel);

            DebugLogger.Log($"BlockResourceCrate.OnBlockInteractStart END -> {result} (base interaction)");
            return result;
        }

        private bool TryHandleOpenDialog(IPlayer byPlayer, BlockEntityResourceCrate be)
        {
            DebugLogger.Log($"BlockResourceCrate.TryHandleOpenDialog START | byPlayerNull={byPlayer == null}, beNull={be == null}");

            if (byPlayer == null || be == null)
            {
                DebugLogger.Log("BlockResourceCrate.TryHandleOpenDialog END -> false (missing player/be)");
                return false;
            }

            bool result = be.TryOpenDialog(byPlayer);

            DebugLogger.Log($"BlockResourceCrate.TryHandleOpenDialog END -> {result}");
            return result;
        }
        
        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            DebugLogger.Log($"BlockResourceCrate.OnPickBlock START | pos={pos}");

            ItemStack result;

            BlockEntityResourceCrate be = GetBlockEntity(world, pos);
            if (be != null)
            {
                result = be.CreateDroppedStack();
                DebugLogger.Log("BlockResourceCrate.OnPickBlock END (from block entity state)");
                return result;
            }

            result = base.OnPickBlock(world, pos);

            DebugLogger.Log("BlockResourceCrate.OnPickBlock END (base)");
            return result;
        }

        public override void OnBlockBroken(
            IWorldAccessor world,
            BlockPos pos,
            IPlayer byPlayer,
            float dropQuantityMultiplier = 1f)
        {
            DebugLogger.Log($"BlockResourceCrate.OnBlockBroken START | pos={pos}, byPlayerNull={byPlayer == null}, dropQuantityMultiplier={dropQuantityMultiplier}");

            BlockEntityResourceCrate be = GetBlockEntity(world, pos);

            if (world != null && world.Side == EnumAppSide.Server && be != null)
            {
                ItemStack droppedStack = be.CreateDroppedStack();

                if (droppedStack != null)
                {
                    world.SpawnItemEntity(droppedStack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    DebugLogger.Log("BlockResourceCrate.OnBlockBroken | Spawned preserved crate itemstack");
                }

                ItemSlot outputSlot = be.ResourceInventory?.OutputSlot;
                if (outputSlot?.Itemstack != null && outputSlot.Itemstack.StackSize > 0)
                {
                    ItemStack contents = outputSlot.Itemstack.Clone();
                    world.SpawnItemEntity(contents, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    outputSlot.Itemstack = null;
                    outputSlot.MarkDirty();
                    DebugLogger.Log("BlockResourceCrate.OnBlockBroken | Spawned crate contents");
                }

                world.BlockAccessor.SetBlock(0, pos);
                DebugLogger.Log("BlockResourceCrate.OnBlockBroken END (custom server break path)");
                return;
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);

            DebugLogger.Log("BlockResourceCrate.OnBlockBroken END (base)");
        }

        public override string GetPlacedBlockInfo(
            IWorldAccessor world,
            BlockPos pos,
            IPlayer forPlayer)
        {
            DebugLogger.Log($"BlockResourceCrate.GetPlacedBlockInfo START | pos={pos}, forPlayerNull={forPlayer == null}");

            string result = base.GetPlacedBlockInfo(world, pos, forPlayer);

            BlockEntityResourceCrate be = GetBlockEntity(world, pos);
            if (be == null)
            {
                DebugLogger.Log($"BlockResourceCrate.GetPlacedBlockInfo END -> {result} (no block entity)");
                return result;
            }

            string targetCode = be.State.TargetItemCode?.ToShortString() ?? "none";
            string extra = $"\nTier: {be.State.CrateTier}\nTarget: {targetCode}";

            result += extra;

            DebugLogger.Log($"BlockResourceCrate.GetPlacedBlockInfo END -> {result}");
            return result;
        }

        private bool TryHandleUpgrade(IPlayer byPlayer, ItemSlot activeHotbarSlot, BlockEntityResourceCrate be)
        {
            DebugLogger.Log($"BlockResourceCrate.TryHandleUpgrade START | byPlayerNull={byPlayer == null}, activeHotbarSlotNull={activeHotbarSlot == null}");

            bool result = false;

            if (byPlayer == null || activeHotbarSlot?.Itemstack == null || be == null)
            {
                DebugLogger.Log("BlockResourceCrate.TryHandleUpgrade END -> false (missing player/slot/be)");
                return false;
            }

            result = be.TryUpgrade(activeHotbarSlot);

            if (result)
            {
                MarkBlockEntityDirty(be);
            }

            DebugLogger.Log($"BlockResourceCrate.TryHandleUpgrade END -> {result}");
            return result;
        }

        private bool TryHandleAssignTarget(IPlayer byPlayer, ItemSlot activeHotbarSlot, BlockEntityResourceCrate be)
        {
            DebugLogger.Log($"BlockResourceCrate.TryHandleAssignTarget START | byPlayerNull={byPlayer == null}, activeHotbarSlotNull={activeHotbarSlot == null}");

            bool result = false;

            if (byPlayer == null || activeHotbarSlot?.Itemstack == null || be == null)
            {
                DebugLogger.Log("BlockResourceCrate.TryHandleAssignTarget END -> false (missing player/slot/be)");
                return false;
            }

            result = be.TryAssignTarget(activeHotbarSlot);

            if (result)
            {
                MarkBlockEntityDirty(be);
            }

            DebugLogger.Log($"BlockResourceCrate.TryHandleAssignTarget END -> {result}");
            return result;
        }

        private bool TryHandleReplaceTarget(IPlayer byPlayer, ItemSlot activeHotbarSlot, BlockEntityResourceCrate be)
        {
            DebugLogger.Log($"BlockResourceCrate.TryHandleReplaceTarget START | byPlayerNull={byPlayer == null}, activeHotbarSlotNull={activeHotbarSlot == null}");

            bool result = false;

            if (byPlayer == null || activeHotbarSlot?.Itemstack == null || be == null)
            {
                DebugLogger.Log("BlockResourceCrate.TryHandleReplaceTarget END -> false (missing player/slot/be)");
                return false;
            }

            result = be.TryReplaceTarget(activeHotbarSlot);

            if (result)
            {
                MarkBlockEntityDirty(be);
            }

            DebugLogger.Log($"BlockResourceCrate.TryHandleReplaceTarget END -> {result}");
            return result;
        }

        private BlockEntityResourceCrate GetBlockEntity(IWorldAccessor world, BlockPos pos)
        {
            DebugLogger.Log($"BlockResourceCrate.GetBlockEntity START | pos={pos}");

            BlockEntityResourceCrate result = null;

            if (world?.BlockAccessor != null && pos != null)
            {
                result = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityResourceCrate;
            }

            DebugLogger.Log($"BlockResourceCrate.GetBlockEntity END -> {(result == null ? "null" : "found")}");
            return result;
        }

        private void MarkBlockEntityDirty(BlockEntityResourceCrate be)
        {
            DebugLogger.Log($"BlockResourceCrate.MarkBlockEntityDirty START | beNull={be == null}");

            if (be != null)
            {
                be.MarkDirty(true);
            }

            DebugLogger.Log("BlockResourceCrate.MarkBlockEntityDirty END");
        }
    }
}