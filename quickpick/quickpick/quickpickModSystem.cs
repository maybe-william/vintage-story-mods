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
using ProtoBuf;

namespace quickpick
{
    
    public class quickpickModSystem : ModSystem
    {
        private Harmony harmony;

        internal static ICoreAPI Api;

        internal static Type PropickType;
        internal static FieldInfo ToolModesField;
        internal static MethodInfo PrintProbeResultsMethod;
        internal static MethodInfo GetToolModeMethod;

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
    
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class QuickPickRequest
    {
        public int X;
        public int Y;
        public int Z;
    }
    
    
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
    
    
    
    
    public static class QuickPickPatches
    {
        public static void OnLoadedPostfix(object __instance, ICoreAPI api)
        {
            if (__instance == null) return;
            if (quickpickModSystem.PropickType == null) return;
            if (!quickpickModSystem.PropickType.IsInstanceOfType(__instance)) return;
            if (quickpickModSystem.ToolModesField == null) return;

            var existingModes = quickpickModSystem.ToolModesField.GetValue(__instance) as SkillItem[];
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
}