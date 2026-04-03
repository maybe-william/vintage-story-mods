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
        internal static MethodInfo PrintProbeResultsMethod;

        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony(Mod.Info.ModID);

            TryResolveRuntimeTargets(api);
            TryApplyPatches(api);

            api.Logger.Notification("[QuickPick] loaded");
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(Mod.Info.ModID);
        }

        private void TryResolveRuntimeTargets(ICoreAPI api)
        {
            // Adjust this string if dnSpy shows a different runtime fullname in 1.21.6
            const string propickFullName = "Vintagestory.GameContent.ItemProspectingPick";

            PropickType =
                AccessTools.TypeByName(propickFullName) ??
                AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(propickFullName, false))
                    .FirstOrDefault(t => t != null);

            if (PropickType == null)
            {
                api.Logger.Warning("[QuickPick] Could not resolve propick type at runtime.");
                return;
            }

            ToolModesField = AccessTools.Field(PropickType, "toolModes");
            PrintProbeResultsMethod = AccessTools.Method(PropickType, "PrintProbeResults");

            api.Logger.Notification($"[QuickPick] Resolved type: {PropickType.FullName}");
            api.Logger.Notification($"[QuickPick] toolModes field found: {ToolModesField != null}");
            api.Logger.Notification($"[QuickPick] PrintProbeResults found: {PrintProbeResultsMethod != null}");
        }

        private void TryApplyPatches(ICoreAPI api)
        {
            if (PropickType == null) return;

            // 1) Patch propick OnLoaded directly
            var onLoaded = AccessTools.Method(PropickType, "OnLoaded");
            if (onLoaded != null)
            {
                harmony.Patch(
                    onLoaded,
                    postfix: new HarmonyMethod(typeof(QuickPickPatches), nameof(QuickPickPatches.PropickOnLoadedPostfix))
                );

                api.Logger.Notification("[QuickPick] Patched propick OnLoaded");
            }
            else
            {
                api.Logger.Warning("[QuickPick] Could not find propick OnLoaded");
            }

            // 2) Right-click patch:
            // If propick declares OnHeldInteractStart, patch that.
            // Otherwise fall back to base CollectibleObject and gate by runtime type.
            var heldInteract = AccessTools.DeclaredMethod(PropickType, "OnHeldInteractStart")
                              ?? AccessTools.Method(typeof(CollectibleObject), "OnHeldInteractStart");

            if (heldInteract != null)
            {
                harmony.Patch(
                    heldInteract,
                    prefix: new HarmonyMethod(typeof(QuickPickPatches), nameof(QuickPickPatches.OnHeldInteractStartPrefix))
                );

                api.Logger.Notification($"[QuickPick] Patched held interact target: {heldInteract.DeclaringType?.FullName}.{heldInteract.Name}");
            }
            else
            {
                api.Logger.Warning("[QuickPick] Could not find held interact target");
            }
        }
    }

    public static class QuickPickPatches
    {
        public static void PropickOnLoadedPostfix(object __instance, ICoreAPI api)
        {
            if (__instance == null) return;
            if (quickpickModSystem.PropickType == null) return;
            if (!quickpickModSystem.PropickType.IsInstanceOfType(__instance)) return;
            if (quickpickModSystem.ToolModesField == null) return;

            var existingModes = quickpickModSystem.ToolModesField.GetValue(__instance) as SkillItem[];
            if (existingModes == null || existingModes.Length == 0) return;

            if (existingModes.Any(m => m?.Code?.Path == "quickpick")) return;

            var newModes = new SkillItem[existingModes.Length + 1];
            Array.Copy(existingModes, newModes, existingModes.Length);

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

            newModes[newModes.Length - 1] = quickpick;
            quickpickModSystem.ToolModesField.SetValue(__instance, newModes);

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
            if (__instance == null) return true;
            if (quickpickModSystem.PropickType == null) return true;
            if (!quickpickModSystem.PropickType.IsInstanceOfType(__instance)) return true;

            if (!firstEvent) return true;
            if (slot?.Itemstack == null) return true;
            if (blockSel == null) return true;
            if (byEntity == null) return true;
            if (quickpickModSystem.ToolModesField == null) return true;
            if (quickpickModSystem.PrintProbeResultsMethod == null) return true;

            var toolModes = quickpickModSystem.ToolModesField.GetValue(__instance) as SkillItem[];
            if (toolModes == null || toolModes.Length == 0) return true;

            int mode = slot.Itemstack.Attributes.GetInt("toolMode");
            if (mode < 0 || mode >= toolModes.Length) return true;
            if (toolModes[mode]?.Code?.Path != "quickpick") return true;

            var world = byEntity.World;
            if (world == null) return true;

            var eplr = byEntity as EntityPlayer;
            if (eplr == null) return true;

            var byPlayer = world.PlayerByUid(eplr.PlayerUID);
            var splr = byPlayer as IServerPlayer;
            if (splr == null) return true;

            quickpickModSystem.PrintProbeResultsMethod.Invoke(
                __instance,
                new object[] { world, splr, slot, blockSel.Position }
            );

            handling = EnumHandHandling.PreventDefault;
            return false;
        }
    }
}