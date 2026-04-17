using resourcecrates.BlockEntities;
using resourcecrates.Util;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace resourcecrates.Blocks
{
    public class BlockResourceCrate : Block
    {
        public override bool OnBlockInteractStart(
            IWorldAccessor world,
            IPlayer byPlayer,
            BlockSelection blockSel)
        {
            DebugLogger.Log(
                $"BlockResourceCrate.OnBlockInteractStart START | player={byPlayer?.PlayerName}, " +
                $"pos={blockSel?.Position}, sneaking={byPlayer?.Entity?.Controls?.Sneak}, " +
                $"held={byPlayer?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible?.Code}"
            );

            if (world == null || byPlayer == null || blockSel == null)
            {
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> false (invalid args)");
                return false;
            }

            BlockPos pos = blockSel.Position;
            IResourceCrateHost be = world.BlockAccessor.GetBlockEntity(pos) as IResourceCrateHost;

            if (be == null)
            {
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> false (BE missing)");
                return false;
            }

            ItemSlot handSlot = byPlayer.InventoryManager?.ActiveHotbarSlot;
            bool sneaking = byPlayer.Entity?.Controls?.Sneak == true;

            // Normal right click:
            // return false so the normal block behavior chain can continue,
            // including container behavior.
            if (!sneaking)
            {
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> false (non-sneak; allow normal behavior chain)");
                bool result = base.OnBlockInteractStart(world, byPlayer, blockSel);
                return result; 
            }

            // Sneak + empty hand: do nothing special for now.
            if (handSlot?.Itemstack == null)
            {
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> false (sneak with empty hand)");
                return false;
            }

            // Client-side prediction:
            // just suppress normal behavior if player is sneak-using with an item.
            if (world.Side == EnumAppSide.Client)
            {
                bool clientHandled = handSlot.Itemstack?.Collectible?.Code != null;

                DebugLogger.Log($"BlockResourceCrate.OnBlockInteractStart END -> {clientHandled} (client prediction)");
                return clientHandled;
            }

            // Server-side priority:
            // 1. Upgrade if valid
            // 2. Otherwise assign/replace target if valid
            if (be.TryUpgrade(byPlayer, handSlot))
            {
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> true (upgrade)");
                return true;
            }

            if (be.TrySetOrReplaceTarget(byPlayer, handSlot))
            {
                DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> true (target set/replace)");
                return true;
            }

            DebugLogger.Log("BlockResourceCrate.OnBlockInteractStart END -> false (no sneak action matched)");
            return false;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            DebugLogger.Log($"BlockResourceCrate.OnPickBlock START | pos={pos}");

            ItemStack stack = base.OnPickBlock(world, pos);

            if (stack == null)
            {
                DebugLogger.Log("BlockResourceCrate.OnPickBlock END -> null");
                return null;
            }

            IResourceCrateHost be = world.BlockAccessor.GetBlockEntity(pos) as IResourceCrateHost;
            be?.WriteCrateStateToItemStack(stack);

            DebugLogger.Log($"BlockResourceCrate.OnPickBlock END | stack={stack.Collectible?.Code}");
            return stack;
        }

        public override void OnBlockBroken(
            IWorldAccessor world,
            BlockPos pos,
            IPlayer byPlayer,
            float dropQuantityMultiplier = 1)
        {
            DebugLogger.Log($"BlockResourceCrate.OnBlockBroken START | pos={pos}, player={byPlayer?.PlayerName}");

            if (world?.Side == EnumAppSide.Server)
            {
                IResourceCrateHost be = world.BlockAccessor.GetBlockEntity(pos) as IResourceCrateHost;

                if (be != null)
                {
                    ItemStack drop = new ItemStack(this);
                    be.WriteCrateStateToItemStack(drop);

                    world.SpawnItemEntity(drop, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    world.BlockAccessor.SetBlock(0, pos);

                    DebugLogger.Log("BlockResourceCrate.OnBlockBroken END (custom server drop)");
                    return;
                }
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            DebugLogger.Log("BlockResourceCrate.OnBlockBroken END (base)");
        }
    }
}