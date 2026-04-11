using Vintagestory.API.Common;
using resourcecrates.Util;

namespace resourcecrates.Inventory
{
    public class ItemSlotResourceCrateOutput : ItemSlot
    {
        public ItemSlotResourceCrateOutput(InventoryBase inventory) : base(inventory)
        {
            DebugLogger.Log("ItemSlotResourceCrateOutput.ctor START");

            DebugLogger.Log("ItemSlotResourceCrateOutput.ctor END");
        }

        public override bool CanTake()
        {
            DebugLogger.Log("ItemSlotResourceCrateOutput.CanTake START");

            bool result = true;

            DebugLogger.Log($"ItemSlotResourceCrateOutput.CanTake END -> {result}");
            return result;
        }

        public override bool CanHold(ItemSlot sourceSlot)
        {
            DebugLogger.Log($"ItemSlotResourceCrateOutput.CanHold START | sourceSlotNull={sourceSlot == null}");

            bool result = false;

            DebugLogger.Log($"ItemSlotResourceCrateOutput.CanHold END -> {result}");
            return result;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            DebugLogger.Log($"ItemSlotResourceCrateOutput.CanTakeFrom START | sourceSlotNull={sourceSlot == null}, priority={priority}");

            bool result = false;

            DebugLogger.Log($"ItemSlotResourceCrateOutput.CanTakeFrom END -> {result}");
            return result;
        }

        public override int TryPutInto(ItemSlot sinkSlot, ref ItemStackMoveOperation op)
        {
            DebugLogger.Log($"ItemSlotResourceCrateOutput.TryPutInto START | sinkSlotNull={sinkSlot == null}, opNull={op == null}");

            int result = base.TryPutInto(sinkSlot, ref op);

            DebugLogger.Log($"ItemSlotResourceCrateOutput.TryPutInto END -> {result}");
            return result;
        }

        public int TryPutGenerated(ItemStack stack)
        {
            DebugLogger.Log($"ItemSlotResourceCrateOutput.TryPutGenerated START | stackNull={stack == null}");

            if (stack == null)
            {
                DebugLogger.Log("ItemSlotResourceCrateOutput.TryPutGenerated END -> 0 (stack null)");
                return 0;
            }

            if (stack.StackSize <= 0)
            {
                DebugLogger.Log("ItemSlotResourceCrateOutput.TryPutGenerated END -> 0 (stack size <= 0)");
                return 0;
            }

            if (Empty)
            {
                Itemstack = stack.Clone();
                int inserted = Itemstack.StackSize;

                MarkDirty();

                DebugLogger.Log($"ItemSlotResourceCrateOutput.TryPutGenerated END -> {inserted} (slot was empty)");
                return inserted;
            }

            if (Itemstack == null)
            {
                DebugLogger.Log("ItemSlotResourceCrateOutput.TryPutGenerated END -> 0 (unexpected null Itemstack)");
                return 0;
            }

            if (!Itemstack.Equals(Inventory.Api.World, stack, GlobalConstants.IgnoredStackAttributes))
            {
                DebugLogger.Log("ItemSlotResourceCrateOutput.TryPutGenerated END -> 0 (stack type mismatch)");
                return 0;
            }

            int maxStackSize = Itemstack.Collectible.MaxStackSize;
            int remainingRoom = maxStackSize - Itemstack.StackSize;

            if (remainingRoom <= 0)
            {
                DebugLogger.Log("ItemSlotResourceCrateOutput.TryPutGenerated END -> 0 (slot full)");
                return 0;
            }

            int toInsert = stack.StackSize <= remainingRoom ? stack.StackSize : remainingRoom;
            Itemstack.StackSize += toInsert;

            MarkDirty();

            DebugLogger.Log($"ItemSlotResourceCrateOutput.TryPutGenerated END -> {toInsert} (merged into existing stack)");
            return toInsert;
        }

        public bool CanAcceptGenerated(ItemStack stack)
        {
            DebugLogger.Log($"ItemSlotResourceCrateOutput.CanAcceptGenerated START | stackNull={stack == null}");

            bool result = false;

            if (stack != null && stack.StackSize > 0)
            {
                if (Empty)
                {
                    result = true;
                }
                else if (Itemstack != null &&
                         Itemstack.Equals(Inventory.Api.World, stack, GlobalConstants.IgnoredStackAttributes) &&
                         Itemstack.StackSize < Itemstack.Collectible.MaxStackSize)
                {
                    result = true;
                }
            }

            DebugLogger.Log($"ItemSlotResourceCrateOutput.CanAcceptGenerated END -> {result}");
            return result;
        }

        public int GetRemainingRoomFor(ItemStack stack)
        {
            DebugLogger.Log($"ItemSlotResourceCrateOutput.GetRemainingRoomFor START | stackNull={stack == null}");

            int result = 0;

            if (stack != null && stack.StackSize > 0)
            {
                if (Empty)
                {
                    result = stack.Collectible.MaxStackSize;
                }
                else if (Itemstack != null &&
                         Itemstack.Equals(Inventory.Api.World, stack, GlobalConstants.IgnoredStackAttributes))
                {
                    result = Itemstack.Collectible.MaxStackSize - Itemstack.StackSize;
                    if (result < 0) result = 0;
                }
            }

            DebugLogger.Log($"ItemSlotResourceCrateOutput.GetRemainingRoomFor END -> {result}");
            return result;
        }
    }
}