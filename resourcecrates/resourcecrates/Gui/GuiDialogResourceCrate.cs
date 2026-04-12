using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using resourcecrates.Inventory;
using resourcecrates.Util;

namespace resourcecrates.Gui
{
    public class GuiDialogResourceCrate : GuiDialogBlockEntityInventory
    {
        private readonly BlockPos _blockEntityPos;
        private readonly InventoryResourceCrate _inventory;

        public GuiDialogResourceCrate(
            string dialogTitle,
            InventoryResourceCrate inventory,
            BlockPos blockEntityPos,
            ICoreClientAPI capi)
            : base(dialogTitle, inventory, blockEntityPos, 1, capi)        {
            DebugLogger.Log("GuiDialogResourceCrate.ctor START");

            _inventory = inventory;
            _blockEntityPos = blockEntityPos;

            SetupDialog();

            DebugLogger.Log("GuiDialogResourceCrate.ctor END");
        }

        private void SetupDialog()
        {
            DebugLogger.Log("GuiDialogResourceCrate.SetupDialog START");

            ElementBounds dialogBounds = ElementBounds.Fixed(0, 0, 240, 120)
                .WithAlignment(EnumDialogArea.RightMiddle);

            ElementBounds bgBounds = dialogBounds.FlatCopy();

            ElementBounds titleBarBounds = dialogBounds.FlatCopy();

            ElementBounds contentBounds = ElementBounds.Fixed(
                GuiStyle.ElementToDialogPadding,
                50,
                80,
                50
            );

            ElementBounds slotBounds = ElementStdBounds.SlotGrid(
                EnumDialogArea.LeftTop,
                0,
                0,
                1,
                1
            );

            SingleComposer = capi.Gui
                .CreateCompo("resourcecrate-" + _blockEntityPos, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Resource Crate", OnTitleBarClose)
                .BeginChildElements(contentBounds)
                .AddItemSlotGrid(_inventory, p => DoSendPacket(p), 1, new[] { 0 }, slotBounds, "outputslot")
                .EndChildElements()
                .Compose();

            DebugLogger.Log("GuiDialogResourceCrate.SetupDialog END");
        }

        private void OnTitleBarClose()
        {
            DebugLogger.Log("GuiDialogResourceCrate.OnTitleBarClose START");

            TryClose();

            DebugLogger.Log("GuiDialogResourceCrate.OnTitleBarClose END");
        }

        public override string ToggleKeyCombinationCode
        {
            get
            {
                DebugLogger.Log("GuiDialogResourceCrate.ToggleKeyCombinationCode START");
                DebugLogger.Log("GuiDialogResourceCrate.ToggleKeyCombinationCode END -> null");
                return null;
            }
        }
        
        public override bool TryClose()
        {
            DebugLogger.Log("GuiDialogResourceCrate.TryClose START");

            bool result = base.TryClose();

            if (result)
            {
                capi.World.Player?.InventoryManager?.CloseInventory(_inventory);
            }

            DebugLogger.Log($"GuiDialogResourceCrate.TryClose END -> {result}");
            return result;
        }
    }
}