using System;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using resourcecrates.Config;
using resourcecrates.BlockEntities;
using resourcecrates.Blocks;
using resourcecrates.Util;

namespace resourcecrates
{
    public class resourcecratesModSystem : ModSystem
    {
        public const string ModId = "notwilliamresourcecrates";
        public const string BlockEntityClassName = "BEResourceCrate";
        public const string BlockClassName = "ResourceCrate";

        public static ResourceCrateResolvedConfig? ResolvedConfig { get; private set; }

        private ICoreAPI? api;
        private ResourceCrateConfigLoader? configLoader;

        public resourcecratesModSystem()
        {
            DebugLogger.Log("ResourceCratesModSystem.ctor START");

            DebugLogger.Log("ResourceCratesModSystem.ctor END");
        }

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            DebugLogger.Log($"ResourceCratesModSystem.ShouldLoad START | forSide={forSide}");

            bool result = true;

            DebugLogger.Log($"ResourceCratesModSystem.ShouldLoad END -> {result}");
            return result;
        }

        public override void Start(ICoreAPI api)
        {
            DebugLogger.Log("ResourceCratesModSystem.Start START");

            this.api = api ?? throw new ArgumentNullException(nameof(api));

            DebugLogger.Init(api);
            DebugLogger.Log("ResourceCratesModSystem.Start | DebugLogger initialized");

            configLoader = new ResourceCrateConfigLoader(api);
            DebugLogger.Log("ResourceCratesModSystem.Start | ResourceCrateConfigLoader created");

            api.RegisterBlockClass(ModId + "." + BlockClassName, typeof(BlockResourceCrate));
            DebugLogger.Log($"ResourceCratesModSystem.Start | Registered block class: {ModId}.resourcecrate");

            api.RegisterBlockEntityClass(ModId + "." + BlockEntityClassName, typeof(BlockEntityResourceCrate));
            DebugLogger.Log($"ResourceCratesModSystem.Start | Registered block entity class: {BlockEntityClassName}");

            DebugLogger.Log("ResourceCratesModSystem.Start END");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            DebugLogger.Log("ResourceCratesModSystem.StartServerSide START");

            if (configLoader == null)
            {
                DebugLogger.Error("ResourceCratesModSystem.StartServerSide | configLoader was null");
                throw new InvalidOperationException("ResourceCrateConfigLoader was not initialized before StartServerSide");
            }

            ResolvedConfig = configLoader.LoadOrCreateResolvedConfig();
            DebugLogger.Log($"ResourceCratesModSystem.StartServerSide | Loaded resolved config: {ResolvedConfig}");

            DebugLogger.Log("ResourceCratesModSystem.StartServerSide END");
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            DebugLogger.Log("ResourceCratesModSystem.StartClientSide START");

            if (configLoader == null)
            {
                DebugLogger.Error("ResourceCratesModSystem.StartClientSide | configLoader was null");
                throw new InvalidOperationException("ResourceCrateConfigLoader was not initialized before StartClientSide");
            }

            if (ResolvedConfig == null)
            {
                DebugLogger.Log("ResourceCratesModSystem.StartClientSide | ResolvedConfig was null, loading client-side copy");
                ResolvedConfig = configLoader.LoadOrCreateResolvedConfig();
            }

            DebugLogger.Log($"ResourceCratesModSystem.StartClientSide | Resolved config available: {ResolvedConfig}");

            DebugLogger.Log("ResourceCratesModSystem.StartClientSide END");
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            DebugLogger.Log("ResourceCratesModSystem.AssetsFinalize START");

            if (ResolvedConfig == null)
            {
                DebugLogger.Warn("ResourceCratesModSystem.AssetsFinalize | ResolvedConfig is still null at finalize time");
            }
            else
            {
                DebugLogger.Log($"ResourceCratesModSystem.AssetsFinalize | ResolvedConfig ready: {ResolvedConfig}");
            }

            DebugLogger.Log("ResourceCratesModSystem.AssetsFinalize END");
        }

        public static ResourceCrateResolvedConfig GetResolvedConfigOrThrow()
        {
            DebugLogger.Log("ResourceCratesModSystem.GetResolvedConfigOrThrow START");

            if (ResolvedConfig == null)
            {
                DebugLogger.Error("ResourceCratesModSystem.GetResolvedConfigOrThrow | ResolvedConfig was null");
                throw new InvalidOperationException("ResourceCrate resolved config has not been initialized");
            }

            DebugLogger.Log($"ResourceCratesModSystem.GetResolvedConfigOrThrow END -> {ResolvedConfig}");
            return ResolvedConfig;
        }

        public override void Dispose()
        {
            DebugLogger.Log("ResourceCratesModSystem.Dispose START");

            api = null;
            configLoader = null;
            ResolvedConfig = null;

            DebugLogger.Log("ResourceCratesModSystem.Dispose END");
        }
    }
}