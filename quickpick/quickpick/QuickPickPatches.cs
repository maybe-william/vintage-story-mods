using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using System;
using System.Linq;

namespace quickpick;

public static class QuickPickPatches
{
    private static QuickPickHarmony harmony = quickpickModSystem.Instance?.harmonySetup;
        
        public static void OnLoadedPostfix(object __instance, ICoreAPI api)
        {
            if (__instance == null) return;
            if (harmony.PropickType == null) return;
            if (!harmony.PropickType.IsInstanceOfType(__instance)) return;
            if (harmony.ToolModesField == null) return;

            var existingModes = harmony.ToolModesField.GetValue(__instance) as SkillItem[];
            if (existingModes == null || existingModes.Length == 0) return;
            if (existingModes.Any(m => m?.Code?.Path == "quickpick")) return;

            var quickpick = new SkillItem
            {
                Code = new AssetLocation("quickpick"),
                Name = "Quickpick Mode"
            };

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

            var newModes = new SkillItem[existingModes.Length + 1];
            Array.Copy(existingModes, newModes, existingModes.Length);
            newModes[newModes.Length - 1] = quickpick;

            harmony.ToolModesField.SetValue(__instance, newModes);
            api.Logger.Notification("[QuickPick] Added quickpick tool mode");
        }

        public static bool OnHeldInteractStartPrefix(
            object __instance,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling)
        {
            if (!firstEvent) return true;

            if (!QuickPickLogic.IsValidQuickPickUse(__instance, slot, byEntity, blockSel, out _))
                return true;

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                quickpickModSystem.ClientChannel?.SendPacket(new QuickPickRequest
                {
                    X = blockSel.Position.X,
                    Y = blockSel.Position.Y,
                    Z = blockSel.Position.Z
                });

                quickpickModSystem.Api?.Logger.Notification("[QuickPick] Sent quickpick packet to server");

                handling = EnumHandHandling.PreventDefault;
                return false;
            }

            return true;
        }
    }