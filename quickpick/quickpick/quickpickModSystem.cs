using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using System;
using HarmonyLib;
using System.Reflection;

namespace quickpick
{
    
    public class quickpickModSystem : ModSystem
    {
        private Harmony harmony;

        internal static ICoreAPI Api;

        internal static Type PropickType;
        internal static FieldInfo ToolModesField;
        internal static MethodInfo GetToolModeMethod;
        
        internal static MethodInfo PrintProbeResultsMethod;
        
        internal static IClientNetworkChannel ClientChannel;
        internal static IServerNetworkChannel ServerChannel;

        public override void Start(ICoreAPI api)
        {
            Api = api;
            harmony = new Harmony(Mod.Info.ModID);

            api.Network
                .RegisterChannel("quickpick")
                .RegisterMessageType(typeof(QuickPickRequest));

            PropickType = AccessTools.TypeByName("Vintagestory.GameContent.ItemProspectingPick");
            if (PropickType == null)
            {
                api.Logger.Warning("[QuickPick] Could not resolve ItemProspectingPick");
                return;
            }

            ToolModesField = AccessTools.Field(PropickType, "toolModes");
            PrintProbeResultsMethod = AccessTools.Method(PropickType, "PrintProbeResults");
            GetToolModeMethod = AccessTools.Method(PropickType, "GetToolMode");

            if (ToolModesField == null || PrintProbeResultsMethod == null || GetToolModeMethod == null)
            {
                api.Logger.Warning("[QuickPick] Could not resolve one or more reflected members");
                return;
            }

            var onLoaded = AccessTools.Method(PropickType, "OnLoaded");
            if (onLoaded == null)
            {
                api.Logger.Warning("[QuickPick] Could not resolve OnLoaded");
                return;
            }

            harmony.Patch(
                onLoaded,
                postfix: new HarmonyMethod(typeof(QuickPickPatches), nameof(QuickPickPatches.OnLoadedPostfix))
            );

            var heldInteract = ResolveHeldInteractTarget(PropickType);
            if (heldInteract == null)
            {
                api.Logger.Warning("[QuickPick] Could not resolve OnHeldInteractStart");
                return;
            }

            harmony.Patch(
                heldInteract,
                prefix: new HarmonyMethod(typeof(QuickPickPatches), nameof(QuickPickPatches.OnHeldInteractStartPrefix))
            );

            api.Logger.Notification($"[QuickPick] Patched OnLoaded");
            api.Logger.Notification($"[QuickPick] Patched held interact: {heldInteract.DeclaringType?.FullName}.{heldInteract.Name}");
            
            // Prepare to patch mapping icon
            PrintProbeResultsMethod = AccessTools.Method(PropickType, "PrintProbeResults");
            GetToolModeMethod = AccessTools.Method(PropickType, "GetToolMode");
            ToolModesField = AccessTools.Field(PropickType, "toolModes");
            
            // Register mapping icon patches
            OreMapLayerPatches.RegisterPatches(harmony, api, PropickType);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            ClientChannel = api.Network.GetChannel("quickpick");
            api.Logger.Notification("[QuickPick] Client channel ready");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            ServerChannel = api.Network.GetChannel("quickpick");
            ServerChannel.SetMessageHandler<QuickPickRequest>(OnQuickPickRequest);
            api.Logger.Notification("[QuickPick] Server channel ready");
        }

        private void OnQuickPickRequest(IServerPlayer fromPlayer, QuickPickRequest msg)
        {
            if (fromPlayer?.Entity == null) return;
            if (PropickType == null || PrintProbeResultsMethod == null) return;

            var activeSlot = fromPlayer.InventoryManager?.ActiveHotbarSlot;
            if (activeSlot?.Itemstack?.Collectible == null) return;

            var item = activeSlot.Itemstack.Collectible;
            var blockSel = new BlockSelection
            {
                Position = new BlockPos(msg.X, msg.Y, msg.Z)
            };

            if (!QuickPickLogic.IsValidQuickPickUse(item, activeSlot, fromPlayer.Entity, blockSel, out _))
                return;
            try
            {
                PrintProbeResultsMethod.Invoke(
                    item,
                    new object[] { fromPlayer.Entity.World, fromPlayer, activeSlot, blockSel.Position }
                );
            }
            catch (Exception ex)
            {
                Api?.Logger.Warning("[QuickPick] PrintProbeResults failed: " + ex.InnerException?.Message ??
                                    ex.Message);
                fromPlayer.SendIngameError("Quickpick is not available in this world type.");
            }

            Api?.Logger.Notification($"[QuickPick] Server executed quickpick at {msg.X},{msg.Y},{msg.Z}");
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(Mod.Info.ModID);
        }

        private static MethodInfo ResolveHeldInteractTarget(Type type)
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