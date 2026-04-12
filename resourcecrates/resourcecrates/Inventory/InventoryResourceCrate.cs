using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using resourcecrates.Util;

namespace resourcecrates.Inventory
{
    public class InventoryResourceCrate : InventoryBase
    {
        public const string InventoryClassNameValue = "resourcecrate";

        private ItemSlot[] slots;

        public InventoryResourceCrate(string inventoryId, ICoreAPI api) : base(inventoryId, api)
        {
            DebugLogger.Log($"InventoryResourceCrate.ctor START | inventoryId={inventoryId}");

            slots = new ItemSlot[1];
            slots[0] = new ItemSlot(this);

            DebugLogger.Log("InventoryResourceCrate.ctor END");
        }

        public ItemSlot OutputSlot
        {
            get
            {
                DebugLogger.Log("InventoryResourceCrate.OutputSlot START");

                ItemSlot result = slots[0];

                DebugLogger.Log("InventoryResourceCrate.OutputSlot END");
                return result;
            }
        }

        public override int Count
        {
            get
            {
                DebugLogger.Log("InventoryResourceCrate.Count START");

                int result = slots.Length;

                DebugLogger.Log($"InventoryResourceCrate.Count END -> {result}");
                return result;
            }
        }

        public string ClassName
        {
            get
            {
                DebugLogger.Log("InventoryResourceCrate.ClassName START");

                string result = InventoryClassNameValue;

                DebugLogger.Log($"InventoryResourceCrate.ClassName END -> {result}");
                return result;
            }
        }

        public override ItemSlot this[int slotId]
        {
            get
            {
                DebugLogger.Log($"InventoryResourceCrate.this[get] START | slotId={slotId}");

                if (slotId < 0 || slotId >= slots.Length)
                {
                    DebugLogger.Error($"InventoryResourceCrate.this[get] | slotId out of range: {slotId}");
                    throw new IndexOutOfRangeException($"Invalid slot id: {slotId}");
                }

                ItemSlot result = slots[slotId];

                DebugLogger.Log("InventoryResourceCrate.this[get] END");
                return result;
            }
            set
            {
                DebugLogger.Log($"InventoryResourceCrate.this[set] START | slotId={slotId}, valueNull={value == null}");

                if (slotId < 0 || slotId >= slots.Length)
                {
                    DebugLogger.Error($"InventoryResourceCrate.this[set] | slotId out of range: {slotId}");
                    throw new IndexOutOfRangeException($"Invalid slot id: {slotId}");
                }

                if (value == null)
                {
                    DebugLogger.Error("InventoryResourceCrate.this[set] | value was null");
                    throw new ArgumentNullException(nameof(value));
                }

                slots[slotId] = value;

                DebugLogger.Log("InventoryResourceCrate.this[set] END");
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            DebugLogger.Log("InventoryResourceCrate.ToTreeAttributes START");

            SlotsToTreeAttributes(slots, tree);

            DebugLogger.Log("InventoryResourceCrate.ToTreeAttributes END");
        }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            DebugLogger.Log("InventoryResourceCrate.FromTreeAttributes START");

            List<ItemSlot> modifiedSlots = new List<ItemSlot>();
            slots = SlotsFromTreeAttributes(tree, slots, modifiedSlots);

            DebugLogger.Log($"InventoryResourceCrate.FromTreeAttributes END | modifiedSlots={modifiedSlots.Count}");
        }

        public override object ActivateSlot(int slotId, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            string side = Api?.Side.ToString() ?? "nullside";
            string invId = InventoryID ?? "nullid";
            int invHash = GetHashCode();

            string beforeTarget;
            if (slotId >= 0 && slotId < slots.Length && slots[slotId]?.Itemstack != null)
            {
                ItemStack stack = slots[slotId].Itemstack;
                beforeTarget = stack.Collectible.Code + " x" + stack.StackSize;
            }
            else
            {
                beforeTarget = "empty";
            }

            string beforeSource;
            if (sourceSlot?.Itemstack != null)
            {
                ItemStack stack = sourceSlot.Itemstack;
                beforeSource = stack.Collectible.Code + " x" + stack.StackSize;
            }
            else
            {
                beforeSource = "empty";
            }

            DebugLogger.Log(
                $"InventoryResourceCrate.ActivateSlot START | " +
                $"side={side}, invId={invId}, invHash={invHash}, " +
                $"slotId={slotId}, sourceSlotNull={sourceSlot == null}, opNull={op == null}, " +
                $"beforeTarget={beforeTarget}, beforeSource={beforeSource}"
            );

            if (slotId == 0 && sourceSlot?.Itemstack != null)
            {
                DebugLogger.Log("InventoryResourceCrate.ActivateSlot END -> null (blocked player insertion into output slot)");
                return null;
            }

            object result = base.ActivateSlot(slotId, sourceSlot, ref op);

            string afterTarget;
            if (slotId >= 0 && slotId < slots.Length && slots[slotId]?.Itemstack != null)
            {
                ItemStack stack = slots[slotId].Itemstack;
                afterTarget = stack.Collectible.Code + " x" + stack.StackSize;
            }
            else
            {
                afterTarget = "empty";
            }

            string afterSource;
            if (sourceSlot?.Itemstack != null)
            {
                ItemStack stack = sourceSlot.Itemstack;
                afterSource = stack.Collectible.Code + " x" + stack.StackSize;
            }
            else
            {
                afterSource = "empty";
            }

            DebugLogger.Log(
                $"InventoryResourceCrate.ActivateSlot END | " +
                $"side={side}, invId={invId}, invHash={invHash}, " +
                $"slotId={slotId}, afterTarget={afterTarget}, afterSource={afterSource}"
            );

            return result;
        }

        public override void LateInitialize(string inventoryId, ICoreAPI api)
        {
            DebugLogger.Log($"InventoryResourceCrate.LateInitialize START | inventoryId={inventoryId}");

            base.LateInitialize(inventoryId, api);

            DebugLogger.Log("InventoryResourceCrate.LateInitialize END");
        }

        public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            DebugLogger.Log($"InventoryResourceCrate.GetSuitability START | sourceSlotNull={sourceSlot == null}, targetSlotNull={targetSlot == null}, isMerge={isMerge}");

            if (targetSlot == slots[0] && sourceSlot?.Itemstack != null)
            {
                DebugLogger.Log("InventoryResourceCrate.GetSuitability END -> 0 (blocked output slot insertion)");
                return 0f;
            }

            float result = base.GetSuitability(sourceSlot, targetSlot, isMerge);

            DebugLogger.Log($"InventoryResourceCrate.GetSuitability END -> {result}");
            return result;
        }

        protected override ItemSlot NewSlot(int slotId)
        {
            DebugLogger.Log($"InventoryResourceCrate.NewSlot START | slotId={slotId}");

            ItemSlot result = new ItemSlot(this);

            DebugLogger.Log("InventoryResourceCrate.NewSlot END");
            return result;
        }

        public override void OnItemSlotModified(ItemSlot slot)
        {
            string slotState;

            if (slot?.Itemstack != null)
            {
                ItemStack stack = slot.Itemstack;
                slotState = stack.Collectible.Code + " x" + stack.StackSize;
            }
            else
            {
                slotState = "empty";
            }

            bool slotNull = slot == null;

            DebugLogger.Log(
                $"InventoryResourceCrate.OnItemSlotModified START | " +
                $"slotNull={slotNull}, " +
                $"slotState={slotState}"
            );

            base.OnItemSlotModified(slot);

            DebugLogger.Log("InventoryResourceCrate.OnItemSlotModified END");
        }
    }
}