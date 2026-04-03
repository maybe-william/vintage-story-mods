using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using System;
using HarmonyLib;

namespace quickpick
{
    public class quickpickModSystem : ModSystem
    {
        private Harmony harmony;

        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony(Mod.Info.ModID);
            harmony.PatchAll();

            api.Logger.Notification("QuickPick loaded");
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(Mod.Info.ModID);
        }
    }
    
    //------------------------------------------------------------------------------
    
    // patch prospecting pick toolModes on loaded?
    [HarmonyPatch(typeof(ItemProspectingPick), "OnLoaded")]
    public static class ItemProspectingPick_OnLoaded_Patch
    {
        private static readonly AccessTools.FieldRef<ItemProspectingPick, SkillItem[]> toolModesRef =
            AccessTools.FieldRefAccess<ItemProspectingPick, SkillItem[]>("toolModes");

        [HarmonyPostfix]
        public static void Postfix(ItemProspectingPick __instance, ICoreAPI api)
        {
            var existingModes = toolModesRef(__instance);
            if (existingModes == null || existingModes.Length == 0) return;

            // Avoid double-adding if something causes OnLoaded to run again
            foreach (var mode in existingModes)
            {
                if (mode?.Code?.Path == "quickpick") return;
            }

            var newModes = new SkillItem[existingModes.Length + 1];
            Array.Copy(existingModes, newModes, existingModes.Length);

            var quickpick = new SkillItem()
            {
                Code = new AssetLocation("quickpick"),
                Name = "Quickpick Mode"
            };

            // Reuse density icon for now
            if (api is ICoreClientAPI capi)
            {
                quickpick.WithIcon(
                    capi,
                    capi.Gui.LoadSvgWithPadding(
                        new AssetLocation("textures/icons/heatmap.svg"),
                        48, 48, 5,
                        ColorUtil.WhiteArgb
                    )
                );
                quickpick.TexturePremultipliedAlpha = false;
            }

            newModes[newModes.Length - 1] = quickpick;
            toolModesRef(__instance) = newModes;
        }
    }
    
    //-------------------------------------------------------------------------------
    
    //patch toolModes again for some reason?
    [HarmonyPatch(typeof(ItemProspectingPick), "GetToolMode")]
    public static class ItemProspectingPick_GetToolMode_Patch
    {
        private static readonly AccessTools.FieldRef<ItemProspectingPick, SkillItem[]> toolModesRef =
            AccessTools.FieldRefAccess<ItemProspectingPick, SkillItem[]>("toolModes");

        [HarmonyPostfix]
        public static void Postfix(ItemProspectingPick __instance, ItemSlot slot, ref int __result)
        {
            var modes = toolModesRef(__instance);
            if (slot?.Itemstack == null || modes == null || modes.Length == 0) return;

            int stored = slot.Itemstack.Attributes.GetInt("toolMode");
            if (stored < 0) stored = 0;
            if (stored >= modes.Length) stored = modes.Length - 1;

            __result = stored;
        }
    }
    
    //-----------------------------------------------------------------------------
    
    //patch on right click method on pro pick specifically
    [HarmonyPatch(typeof(ItemProspectingPick), "OnHeldInteractStart")]
    public static class ItemProspectingPick_OnHeldInteractStart_Patch
    {
        private static readonly AccessTools.FieldRef<ItemProspectingPick, SkillItem[]> toolModesRef =
            AccessTools.FieldRefAccess<ItemProspectingPick, SkillItem[]>("toolModes");

        private static readonly MethodInfo printProbeResultsMethod =
            AccessTools.Method(typeof(ItemProspectingPick), "PrintProbeResults");

        [HarmonyPrefix]
        public static bool Prefix(
            ItemProspectingPick __instance,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling
        )
        {
            if (!firstEvent) return true;
            if (slot?.Itemstack == null) return true;
            if (blockSel == null) return true;
            if (byEntity == null) return true;

            var world = byEntity.World;
            if (world == null) return true;

            var playerEntity = byEntity as EntityPlayer;
            if (playerEntity == null) return true;

            var byPlayer = world.PlayerByUid(playerEntity.PlayerUID);
            if (byPlayer == null) return true;

            var toolModes = toolModesRef(__instance);
            if (toolModes == null || toolModes.Length == 0) return true;

            int mode = slot.Itemstack.Attributes.GetInt("toolMode");
            if (mode < 0 || mode >= toolModes.Length) return true;

            if (toolModes[mode]?.Code?.Path != "quickpick") return true;

            var splr = byPlayer as IServerPlayer;
            if (splr == null)
            {
                // Let client continue; actual authoritative work should happen server-side
                return true;
            }

            // Call the same protected result pipeline vanilla density mode eventually uses
            printProbeResultsMethod.Invoke(
                __instance,
                new object[] { world, splr, slot, blockSel.Position }
            );

            handling = EnumHandHandling.PreventDefault;
            return false;
        }
    }
}