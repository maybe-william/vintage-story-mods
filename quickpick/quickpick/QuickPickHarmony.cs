using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;

namespace quickpick
{
    public class QuickPickHarmony : IDisposable
    {
        private readonly Harmony harmony;
        private readonly string modId;

        internal Type PropickType;
        internal  FieldInfo ToolModesField;
        internal  MethodInfo GetToolModeMethod;
        internal  MethodInfo PrintProbeResultsMethod;

        internal bool IsReady =>
            PropickType != null &&
            ToolModesField != null &&
            GetToolModeMethod != null &&
            PrintProbeResultsMethod != null;

        public QuickPickHarmony(string modId)
        {
            this.modId = modId;
            harmony = new Harmony(modId);
        }

        public bool TryPatchAll(ICoreAPI api)
        {
            if (!ResolveMembers(api)) return false;
            return PatchMethods(api);
        }

        public void Dispose()
        {
            harmony?.UnpatchAll(modId);
        }

        private bool ResolveMembers(ICoreAPI api)
        {
            PropickType = AccessTools.TypeByName("Vintagestory.GameContent.ItemProspectingPick");
            if (PropickType == null)
            {
                api.Logger.Warning("[QuickPick] Could not resolve ItemProspectingPick");
                return false;
            }

            ToolModesField = AccessTools.Field(PropickType, "toolModes");
            PrintProbeResultsMethod = AccessTools.Method(PropickType, "PrintProbeResults");
            GetToolModeMethod = AccessTools.Method(PropickType, "GetToolMode");

            if (!IsReady)
            {
                api.Logger.Warning("[QuickPick] Could not resolve one or more reflected members");
                return false;
            }

            return true;
        }

        private bool PatchMethods(ICoreAPI api)
        {
            var onLoaded = AccessTools.Method(PropickType, "OnLoaded");
            if (onLoaded == null)
            {
                api.Logger.Warning("[QuickPick] Could not resolve OnLoaded");
                return false;
            }

            harmony.Patch(
                onLoaded,
                postfix: new HarmonyMethod(typeof(QuickPickPatches), nameof(QuickPickPatches.OnLoadedPostfix))
            );

            var heldInteract = ResolveHeldInteractTarget(PropickType);
            if (heldInteract == null)
            {
                api.Logger.Warning("[QuickPick] Could not resolve OnHeldInteractStart");
                return false;
            }

            harmony.Patch(
                heldInteract,
                prefix: new HarmonyMethod(typeof(QuickPickPatches), nameof(QuickPickPatches.OnHeldInteractStartPrefix))
            );

            api.Logger.Notification("[QuickPick] Patched OnLoaded");
            api.Logger.Notification(
                $"[QuickPick] Patched held interact: {heldInteract.DeclaringType?.FullName}.{heldInteract.Name}"
            );

            return true;
        }

        private MethodInfo ResolveHeldInteractTarget(Type type)
        {
            while (type != null)
            {
                var method = AccessTools.DeclaredMethod(type, "OnHeldInteractStart");
                if (method != null) return method;
                type = type.BaseType;
            }

            return null;
        }
    }
}