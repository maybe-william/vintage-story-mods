using Vintagestory.API.Common;
using Vintagestory.API.Client;

namespace quickpick;

public static class QuickPickLogic
{
    public static bool IsValidQuickPickUse(
        object instance,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        out IPlayer byPlayer)
    {
        byPlayer = null;

        // Not even the propick? Ignore silently.
        if (instance == null) return false;
        if (quickpickModSystem.PropickType == null) return false;
        if (!quickpickModSystem.PropickType.IsInstanceOfType(instance)) return false;

        // From here on, we know it's the propick, so logging is useful.
        if (slot?.Itemstack == null)
        {
            Log("Invalid quickpick use: slot or itemstack was null");
            return false;
        }

        if (byEntity == null)
        {
            Log("Invalid quickpick use: byEntity was null");
            return false;
        }

        if (blockSel == null)
        {
            Log("Invalid quickpick use: blockSel was null");
            return false;
        }

        if (quickpickModSystem.GetToolModeMethod == null)
        {
            Log("Invalid quickpick use: GetToolModeMethod was null");
            return false;
        }

        if (quickpickModSystem.ToolModesField == null)
        {
            Log("Invalid quickpick use: ToolModesField was null");
            return false;
        }

        var eplr = byEntity as EntityPlayer;
        if (eplr == null)
        {
            Log("Invalid quickpick use: byEntity was not an EntityPlayer");
            return false;
        }

        byPlayer = byEntity.World?.PlayerByUid(eplr.PlayerUID);
        if (byPlayer == null)
        {
            Log("Invalid quickpick use: could not resolve player from UID");
            return false;
        }

        int mode;
        try
        {
            mode = (int)quickpickModSystem.GetToolModeMethod.Invoke(
                instance,
                new object[] { slot, byPlayer, blockSel }
            );
        }
        catch (System.Exception ex)
        {
            Log("Invalid quickpick use: GetToolMode invoke failed: " + ex.Message);
            return false;
        }

        var modes = quickpickModSystem.ToolModesField.GetValue(instance) as SkillItem[];
        if (modes == null)
        {
            Log("Invalid quickpick use: toolModes was null");
            return false;
        }

        if (mode < 0 || mode >= modes.Length)
        {
            Log($"Invalid quickpick use: mode index {mode} out of range for toolModes length {modes.Length}");
            return false;
        }

        var modeCode = modes[mode]?.Code?.Path;
        if (modeCode != "quickpick")
        {
            // Correct item, wrong mode: still useful to know, but concise.
            Log($"Invalid quickpick use: active mode was '{modeCode ?? "null"}', not 'quickpick'");
            return false;
        }

        return true;
    }

    private static void Log(string message)
    {
        quickpickModSystem.Api?.Logger.Notification("[QuickPick] " + message);
    }
}