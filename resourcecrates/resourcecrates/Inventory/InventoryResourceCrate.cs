using System;
using Vintagestory.API.Common;
using resourcecrates.Util;

namespace resourcecrates.Inventory
{
    public class InventoryResourceCrate : InventoryBase
    {
        public const string InventoryClassNameValue = "resourcecrate";

        private readonly ItemSlot[] slots;

        public InventoryResourceCrate(string inventoryId, ICoreAPI api) : base(inventoryId, api)
        {
            DebugLogger.Log($"InventoryResourceCrate.ctor START | inventoryId={inventoryId}");

            slots = new ItemSlot[1];
            slots[0] = new ItemSlotResourceCrateOutput(this);

            DebugLogger.Log("InventoryResourceCrate.ctor END");
        }

        public ItemSlotResourceCrateOutput OutputSlot
        {
            get
            {
                DebugLogger.Log("InventoryResourceCrate.OutputSlot START");

                ItemSlotResourceCrateOutput result = (ItemSlotResourceCrateOutput)slots[0];

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

        public override string ClassName
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

        public override object ActivateSlot(int slotId, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            DebugLogger.Log($"InventoryResourceCrate.ActivateSlot START | slotId={slotId}, sourceSlotNull={sourceSlot == null}, opNull={op == null}");

            object result = base.ActivateSlot(slotId, sourceSlot, ref op);

            DebugLogger.Log("InventoryResourceCrate.ActivateSlot END");
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

            float result = base.GetSuitability(sourceSlot, targetSlot, isMerge);

            DebugLogger.Log($"InventoryResourceCrate.GetSuitability END -> {result}");
            return result;
        }

        protected override ItemSlot NewSlot(int slotId)
        {
            DebugLogger.Log($"InventoryResourceCrate.NewSlot START | slotId={slotId}");

            ItemSlot result = new ItemSlotResourceCrateOutput(this);

            DebugLogger.Log("InventoryResourceCrate.NewSlot END");
            return result;
        }

        public override void OnItemSlotModified(ItemSlot slot)
        {
            DebugLogger.Log($"InventoryResourceCrate.OnItemSlotModified START | slotNull={slot == null}");

            base.OnItemSlotModified(slot);

            DebugLogger.Log("InventoryResourceCrate.OnItemSlotModified END");
        }
    }
}