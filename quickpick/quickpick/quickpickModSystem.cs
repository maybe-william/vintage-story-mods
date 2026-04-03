using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using System;
using System.Linq;
using HarmonyLib;
using System.Reflection;

namespace quickpick
{
    public class quickpickModSystem : ModSystem
    {
        private Harmony harmony;

        internal static Type PropickType;
        internal static FieldInfo ToolModesField;
        internal static ICoreAPI Api;
        internal static ItemQuickProPick QuickProPick;

        public override void Start(ICoreAPI api)
        {
            Api = api;
            harmony = new Harmony(Mod.Info.ModID);

            PropickType = AccessTools.TypeByName("Vintagestory.GameContent.ItemProspectingPick");
            if (PropickType == null)
            {
                api.Logger.Warning("[QuickPick] Could not resolve ItemProspectingPick");
                return;
            }

            ToolModesField = AccessTools.Field(PropickType, "toolModes");
            if (ToolModesField == null)
            {
                api.Logger.Warning("[QuickPick] Could not resolve toolModes field");
                return;
            }

            QuickProPick = new ItemQuickProPick(api);

            harmony.Patch(
                AccessTools.Method(PropickType, "GetToolModes"),
                postfix: new HarmonyMethod(typeof(QuickPickPatches), nameof(QuickPickPatches.GetToolModesPostfix))
            );

            harmony.Patch(
                AccessTools.Method(PropickType, "GetToolMode"),
                postfix: new HarmonyMethod(typeof(QuickPickPatches), nameof(QuickPickPatches.GetToolModePostfix))
            );

            harmony.Patch(
                AccessTools.Method(PropickType, "SetToolMode"),
                prefix: new HarmonyMethod(typeof(QuickPickPatches), nameof(QuickPickPatches.SetToolModePrefix))
            );

            api.Logger.Notification("[QuickPick] Virtual propick mode patches applied");
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(Mod.Info.ModID);
        }
    }


    /// <summary>
    /// Virtual subclass to be patched in for get/set toolMode(s), always appending custom toolMode
    /// </summary>
    public class ItemQuickProPick
    {
        private readonly ICoreAPI api;
        private SkillItem quickpickMode;

        public ItemQuickProPick(ICoreAPI api)
        {
            this.api = api;
        }

        private SkillItem QuickpickMode
        {
            get
            {
                if (quickpickMode != null) return quickpickMode;

                quickpickMode = new SkillItem
                {
                    Code = new AssetLocation("quickpick"),
                    Name = "Quickpick Mode"
                };

                if (api is ICoreClientAPI capi)
                {
                    quickpickMode.WithIcon(
                        capi,
                        capi.Gui.LoadSvgWithPadding(
                            new AssetLocation("textures/icons/heatmap.svg"),
                            48, 48, 5,
                            ColorUtil.WhiteArgb
                        )
                    );
                    quickpickMode.TexturePremultipliedAlpha = false;
                }

                return quickpickMode;
            }
        }

        private SkillItem[] RawModes(object instance)
        {
            return quickpickModSystem.ToolModesField?.GetValue(instance) as SkillItem[];
        }

        public SkillItem[] GetToolModes(object instance)
        {
            var raw = RawModes(instance);
            if (raw == null || raw.Length == 0) return raw;

            if (raw.Any(m => m?.Code?.Path == "quickpick")) return raw;

            var result = new SkillItem[raw.Length + 1];
            Array.Copy(raw, result, raw.Length);
            result[result.Length - 1] = QuickpickMode;
            return result;
        }

        public int GetToolMode(object instance, ItemSlot slot)
        {
            var modes = GetToolModes(instance);
            if (modes == null || modes.Length == 0) return 0;

            int mode = slot?.Itemstack?.Attributes?.GetInt("toolMode") ?? 0;

            if (mode < 0) mode = 0;
            if (mode >= modes.Length) mode = modes.Length - 1;

            return mode;
        }

        public void SetToolMode(object instance, ItemSlot slot, int toolMode)
        {
            var modes = GetToolModes(instance);
            if (slot?.Itemstack == null || modes == null || modes.Length == 0) return;

            if (toolMode < 0) toolMode = 0;
            if (toolMode >= modes.Length) toolMode = modes.Length - 1;

            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
        }
    }

    /// <summary>
    /// for the literal patching in of the "virtual subclass"
    /// </summary>
    public static class QuickPickPatches
    {
        public static void GetToolModesPostfix(object __instance, ref SkillItem[] __result)
        {
            if (!quickpickModSystem.PropickType.IsInstanceOfType(__instance)) return;
            __result = quickpickModSystem.QuickProPick.GetToolModes(__instance);
        }

        public static void GetToolModePostfix(object __instance, ItemSlot slot, ref int __result)
        {
            if (!quickpickModSystem.PropickType.IsInstanceOfType(__instance)) return;
            __result = quickpickModSystem.QuickProPick.GetToolMode(__instance, slot);
        }

        public static bool SetToolModePrefix(object __instance, ItemSlot slot, int toolMode)
        {
            if (!quickpickModSystem.PropickType.IsInstanceOfType(__instance)) return true;

            quickpickModSystem.QuickProPick.SetToolMode(__instance, slot, toolMode);
            return false;
        }
    }
}