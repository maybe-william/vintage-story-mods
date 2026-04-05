using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using System;

namespace quickpick
{
    public class quickpickModSystem : ModSystem
    {
        internal static ICoreAPI Api;
        internal static IClientNetworkChannel ClientChannel;
        internal static IServerNetworkChannel ServerChannel;

        public QuickPickHarmony harmonySetup;
        
        internal static quickpickModSystem Instance { get; private set; }

        
        public override void Start(ICoreAPI api)
        {
            Instance = this;
            Api = api;

            RegisterNetwork(api);
            harmonySetup = new QuickPickHarmony(Mod.Info.ModID);
            harmonySetup.TryPatchAll(api);
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

        public override void Dispose()
        {
            harmonySetup?.Dispose();
        }

        private static void RegisterNetwork(ICoreAPI api)
        {
            api.Network
                .RegisterChannel("quickpick")
                .RegisterMessageType(typeof(QuickPickRequest));
        }

        private void OnQuickPickRequest(IServerPlayer fromPlayer, QuickPickRequest msg)
        {
            if (fromPlayer?.Entity == null) return;
            if (!harmonySetup.IsReady) return;

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
                harmonySetup.PrintProbeResultsMethod.Invoke(
                    item,
                    new object[] { fromPlayer.Entity.World, fromPlayer, activeSlot, blockSel.Position }
                );
                
                // damage the pick by 6 durability
                item.DamageItem(fromPlayer.Entity.World, fromPlayer.Entity, activeSlot, 6);
                activeSlot.MarkDirty();
            }
            catch (Exception ex)
            {
                Api?.Logger.Warning(
                    "[QuickPick] PrintProbeResults failed: " +
                    (ex.InnerException?.Message ?? ex.Message)
                );
                fromPlayer.SendIngameError("Quickpick is not available in this world type.");
            }

            Api?.Logger.Notification($"[QuickPick] Server executed quickpick at {msg.X},{msg.Y},{msg.Z}");
        }
    }
}