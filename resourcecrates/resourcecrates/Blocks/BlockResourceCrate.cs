using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using resourcecrates.Runtime;
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
            DebugLogger.Log(
                $"BlockResourceCrate.OnBlockInteractStart START | " +
                $"byPlayerNull={byPlayer == null}, blockSelNull={blockSel == null}"
            );

            if (world == null || byPlayer == null || blockSel == null)
            {
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> false (null argument)");
                return false;
            }

            object? be = GetBlockEntityObject(world, blockSel.Position);
            if (be == null)
            {
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> false (no block entity)");
                return false;
            }

            if (!ResourceCrateRuntimeHelpers.IsResourceCrateContainer(be))
            {
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> false (not resource crate container)");
                return false;
            }

            ItemSlot? activeHotbarSlot = byPlayer.InventoryManager?.ActiveHotbarSlot;
            bool isSneaking = byPlayer.Entity.Controls.ShiftKey;

            DebugLogger.Log(
                $"BlockResourceCrate.OnBlockInteractStart | " +
                $"isSneaking={isSneaking}, heldItemNull={activeHotbarSlot?.Itemstack == null}"
            );

            if (isSneaking)
            {
                if (ResourceCrateRuntimeInteractions.TryUpgrade(be, activeHotbarSlot))
                {
                    DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> true (shift-upgrade handled)");
                    return true;
                }

                if (ResourceCrateRuntimeInteractions.TryAssignTarget(be, activeHotbarSlot))
                {
                    DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> true (shift-assign target handled)");
                    return true;
                }

                if (ResourceCrateRuntimeInteractions.TryReplaceTarget(be, activeHotbarSlot))
                {
                    DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> true (shift-replace target handled)");
                    return true;
                }

                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> false (shift held, no valid crate action)");
                return false;
            }

            bool result = base.OnBlockInteractStart(world, byPlayer, blockSel);

            DebugLogger.Log($"BlockResourceCrate.OnBlockInteractStart END -> {result} (base interaction)");
            return result;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            DebugLogger.Log($"BlockResourceCrate.OnPickBlock START | pos={pos}");

            object? be = GetBlockEntityObject(world, pos);
            if (be != null && ResourceCrateRuntimeHelpers.IsResourceCrateContainer(be))
            {
                ResourceCrateRuntimeState runtime = ResourceCrateRuntimeState.GetOrCreate(be);
                ItemStack result = ResourceCrateRuntimeInteractions.CreateDroppedStack(world, this, runtime);

                DebugLogger.Log("BlockResourceCrate.OnPickBlock END (from runtime state)");
                return result;
            }

            ItemStack baseResult = base.OnPickBlock(world, pos);

            DebugLogger.Log("BlockResourceCrate.OnPickBlock END (base)");
            return baseResult;
        }

        public override void OnBlockBroken(
            IWorldAccessor world,
            BlockPos pos,
            IPlayer byPlayer,
            float dropQuantityMultiplier = 1f)
        {
            DebugLogger.Log(
                $"BlockResourceCrate.OnBlockBroken START | " +
                $"pos={pos}, byPlayerNull={byPlayer == null}, dropQuantityMultiplier={dropQuantityMultiplier}"
            );

            object? be = GetBlockEntityObject(world, pos);

            if (world != null && world.Side == EnumAppSide.Server && be != null && ResourceCrateRuntimeHelpers.IsResourceCrateContainer(be))
            {
                ResourceCrateRuntimeState runtime = ResourceCrateRuntimeState.GetOrCreate(be);

                ItemStack droppedStack = ResourceCrateRuntimeInteractions.CreateDroppedStack(world, this, runtime);
                if (droppedStack != null)
                {
                    world.SpawnItemEntity(droppedStack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    DebugLogger.Log("BlockResourceCrate.OnBlockBroken | Spawned preserved crate itemstack");
                }

                InventoryBase? inventory = ResourceCrateRuntimeHelpers.GetInventory(be);
                ItemSlot? outputSlot = inventory == null ? null : ResourceCrateRuntimeHelpers.GetSlot(inventory, 0);

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

            object? be = GetBlockEntityObject(world, pos);
            if (be == null || !ResourceCrateRuntimeHelpers.IsResourceCrateContainer(be))
            {
                DebugLogger.Log($"BlockResourceCrate.GetPlacedBlockInfo END -> {result} (no compatible block entity)");
                return result;
            }

            ResourceCrateRuntimeState runtime = ResourceCrateRuntimeState.GetOrCreate(be);
            string targetCode = runtime.State.TargetItemCode?.ToShortString() ?? "none";
            string extra = $"\nTier: {runtime.State.CrateTier}\nTarget: {targetCode}";

            result += extra;

            DebugLogger.Log($"BlockResourceCrate.GetPlacedBlockInfo END -> {result}");
            return result;
        }

        private object? GetBlockEntityObject(IWorldAccessor world, BlockPos pos)
        {
            DebugLogger.Log($"BlockResourceCrate.GetBlockEntityObject START | pos={pos}");

            object? result = null;

            if (world?.BlockAccessor != null && pos != null)
            {
                result = world.BlockAccessor.GetBlockEntity(pos);
            }

            DebugLogger.Log(
                $"BlockResourceCrate.GetBlockEntityObject END -> " +
                $"{(result == null ? "null" : result.GetType().FullName)}"
            );

            return result;
        }
    }
}