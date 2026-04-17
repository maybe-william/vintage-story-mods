using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace resourcecrates.BlockEntities
{
    /// <summary>
    /// Bridge implemented by the runtime-generated block entity.
    ///
    /// All access is exposed as methods so the controller/block always reads
    /// the live current value from the generated BE instance at call time.
    ///
    /// This interface includes:
    /// 1. Host access needed by the plain C# BlockEntityResourceCrate controller
    /// 2. Block-facing BE methods that BlockResourceCrate will call directly
    /// </summary>
    public interface IResourceCrateHost
    {
        // ---------------------------------------------------------------------
        // Live BE / container / world access
        // ---------------------------------------------------------------------

        ICoreAPI GetApi();

        ItemSlot GetOutputSlot();

        void CallMarkDirty(bool redrawOnClient = false);

        long CallRegisterGameTickListener(Action<float> onTick, int intervalMs, int initialDelayMs = 0);
        
        void CallUnregisterGameTickListener(long listenerId);


        // ---------------------------------------------------------------------
        // Explicit base-dispatch wrappers
        // Only the generated subclass can legally call base.X(...)
        // ---------------------------------------------------------------------

        void CallBaseInitialize(ICoreAPI api);

        void CallBaseOnBlockPlaced(ItemStack byItemStack = null);

        void CallBaseFromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving);

        void CallBaseToTreeAttributes(ITreeAttribute tree);

        void CallBaseOnBlockUnloaded();

        void CallBaseOnBlockRemoved();

        void CallBaseGetBlockInfo(IPlayer forPlayer, StringBuilder dsc);


        // ---------------------------------------------------------------------
        // Block-facing resource crate behavior
        // These are called by BlockResourceCrate.
        // The generated BE should implement these by forwarding into the
        // plain C# BlockEntityResourceCrate controller.
        // ---------------------------------------------------------------------

        bool TryUpgrade(IPlayer byPlayer, ItemSlot handSlot);

        bool TrySetOrReplaceTarget(IPlayer byPlayer, ItemSlot handSlot);

        void WriteCrateStateToItemStack(ItemStack stack);
    }
}