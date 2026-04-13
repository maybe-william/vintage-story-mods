using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using resourcecrates.Runtime;
using resourcecrates.Util;

namespace resourcecrates.Patches
{
    public static class BlockEntityGenericContainerPatches
    {
        public static void Apply(Harmony harmony)
        {
            DebugLogger.Log("BlockEntityGenericContainerPatches.Apply START");

            harmony.CreateClassProcessor(typeof(InitializePatch)).Patch();
            harmony.CreateClassProcessor(typeof(ToTreeAttributesPatch)).Patch();
            harmony.CreateClassProcessor(typeof(FromTreeAttributesPatch)).Patch();
            harmony.CreateClassProcessor(typeof(OnBlockRemovedPatch)).Patch();
            harmony.CreateClassProcessor(typeof(OnBlockUnloadedPatch)).Patch();

            DebugLogger.Log("BlockEntityGenericContainerPatches.Apply END");
        }

        private static Type? GetTargetType()
        {
            return AccessTools.TypeByName("Vintagestory.GameContent.BlockEntityGenericContainer");
        }
        

        private static ICoreAPI? GetApi(object beInstance)
        {
            return AccessTools.Property(beInstance.GetType(), "Api")?.GetValue(beInstance) as ICoreAPI;
        }

        private static BlockPos? GetPos(object beInstance)
        {
            return AccessTools.Property(beInstance.GetType(), "Pos")?.GetValue(beInstance) as BlockPos;
        }

        private static long RegisterTickListener(object beInstance)
        {
            MethodInfo? method = AccessTools.Method(
                beInstance.GetType(),
                "RegisterGameTickListener",
                new[] { typeof(Action<float>), typeof(int) }
            );

            if (method == null)
            {
                DebugLogger.Error("BlockEntityGenericContainerPatches.RegisterTickListener | method not found");
                return -1;
            }

            Action<float> callback = dt => ResourceCrateRuntimeTicker.OnServerTick(beInstance, dt);

            object? result = method.Invoke(beInstance, new object[] { callback, 1000 });

            long tickId = result is long l ? l : -1;

            DebugLogger.Log($"BlockEntityGenericContainerPatches.RegisterTickListener | tickId={tickId}");

            return tickId;
        }

        private static void UnregisterTickListener(object beInstance, long tickId)
        {
            if (tickId < 0) return;

            MethodInfo? method = AccessTools.Method(
                beInstance.GetType(),
                "UnregisterGameTickListener",
                new[] { typeof(long) }
            );

            if (method == null)
            {
                DebugLogger.Error("BlockEntityGenericContainerPatches.UnregisterTickListener | method not found");
                return;
            }

            method.Invoke(beInstance, new object[] { tickId });

            DebugLogger.Log($"BlockEntityGenericContainerPatches.UnregisterTickListener | tickId={tickId}");
        }

        [HarmonyPatch]
        public static class InitializePatch
        {
            public static MethodBase? TargetMethod()
            {
                DebugLogger.Log("BlockEntityGenericContainerPatches.InitializePatch.TargetMethod START");

                Type? targetType = GetTargetType();
                if (targetType == null)
                {
                    DebugLogger.Error("BlockEntityGenericContainerPatches.InitializePatch.TargetMethod | target type not found");
                    return null;
                }

                MethodInfo? method = AccessTools.Method(targetType, "Initialize", new[] { typeof(ICoreAPI) });

                DebugLogger.Log($"BlockEntityGenericContainerPatches.InitializePatch.TargetMethod END -> found={method != null}");
                return method;
            }

            public static void Postfix(object __instance, ICoreAPI api)
            {
                try
                {
                    DebugLogger.Log("BlockEntityGenericContainerPatches.InitializePatch.Postfix START");

                    if (__instance == null || api == null)
                    {
                        DebugLogger.Log("BlockEntityGenericContainerPatches.InitializePatch.Postfix END (null instance/api)");
                        return;
                    }

                    if (!ResourceCrateRuntimeHelpers.IsResourceCrateContainer(__instance))
                    {
                        DebugLogger.Log("BlockEntityGenericContainerPatches.InitializePatch.Postfix END (not resource crate)");
                        return;
                    }

                    ResourceCrateRuntimeState runtime = ResourceCrateRuntimeState.GetOrCreate(__instance);
                    runtime.LastKnownPos = GetPos(__instance);

                    if (!runtime.IsInitialized)
                    {
                        runtime.EnsurePersistentId();

                        if (api.Side == EnumAppSide.Server)
                        {
                            runtime.State.LastUpdateTotalHours = api.World?.Calendar?.TotalHours ?? 0;
                            runtime.TickListenerId = RegisterTickListener(__instance);
                        }

                        runtime.IsInitialized = true;
                    }

                    DebugLogger.Log(
                        $"BlockEntityGenericContainerPatches.InitializePatch.Postfix END | runtime={runtime}"
                    );
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"BlockEntityGenericContainerPatches.InitializePatch.Postfix EXCEPTION | {ex}");
                }
            }
        }

        [HarmonyPatch]
        public static class ToTreeAttributesPatch
        {
            public static MethodBase? TargetMethod()
            {
                DebugLogger.Log("BlockEntityGenericContainerPatches.ToTreeAttributesPatch.TargetMethod START");

                Type? targetType = GetTargetType();
                if (targetType == null)
                {
                    DebugLogger.Error("BlockEntityGenericContainerPatches.ToTreeAttributesPatch.TargetMethod | target type not found");
                    return null;
                }

                MethodInfo? method = AccessTools.Method(targetType, "ToTreeAttributes", new[] { typeof(ITreeAttribute) });

                DebugLogger.Log($"BlockEntityGenericContainerPatches.ToTreeAttributesPatch.TargetMethod END -> found={method != null}");
                return method;
            }

            public static void Postfix(object __instance, ITreeAttribute tree)
            {
                try
                {
                    DebugLogger.Log("BlockEntityGenericContainerPatches.ToTreeAttributesPatch.Postfix START");

                    if (__instance == null || tree == null)
                    {
                        DebugLogger.Log("BlockEntityGenericContainerPatches.ToTreeAttributesPatch.Postfix END (null instance/tree)");
                        return;
                    }

                    if (!ResourceCrateRuntimeHelpers.IsResourceCrateContainer(__instance))
                    {
                        DebugLogger.Log("BlockEntityGenericContainerPatches.ToTreeAttributesPatch.Postfix END (not resource crate)");
                        return;
                    }

                    ResourceCrateRuntimeState runtime = ResourceCrateRuntimeState.GetOrCreate(__instance);
                    runtime.WriteToTree(tree);

                    DebugLogger.Log(
                        $"BlockEntityGenericContainerPatches.ToTreeAttributesPatch.Postfix END | runtime={runtime}"
                    );
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"BlockEntityGenericContainerPatches.ToTreeAttributesPatch.Postfix EXCEPTION | {ex}");
                }
            }
        }

        [HarmonyPatch]
        public static class FromTreeAttributesPatch
        {
            public static MethodBase? TargetMethod()
            {
                DebugLogger.Log("BlockEntityGenericContainerPatches.FromTreeAttributesPatch.TargetMethod START");

                Type? targetType = GetTargetType();
                if (targetType == null)
                {
                    DebugLogger.Error("BlockEntityGenericContainerPatches.FromTreeAttributesPatch.TargetMethod | target type not found");
                    return null;
                }

                MethodInfo? method = AccessTools.Method(
                    targetType,
                    "FromTreeAttributes",
                    new[] { typeof(ITreeAttribute), typeof(IWorldAccessor) }
                );

                DebugLogger.Log($"BlockEntityGenericContainerPatches.FromTreeAttributesPatch.TargetMethod END -> found={method != null}");
                return method;
            }

            public static void Postfix(object __instance, ITreeAttribute tree, IWorldAccessor worldForResolving)
            {
                try
                {
                    DebugLogger.Log("BlockEntityGenericContainerPatches.FromTreeAttributesPatch.Postfix START");

                    if (__instance == null || tree == null)
                    {
                        DebugLogger.Log("BlockEntityGenericContainerPatches.FromTreeAttributesPatch.Postfix END (null instance/tree)");
                        return;
                    }

                    if (!ResourceCrateRuntimeHelpers.IsResourceCrateContainer(__instance))
                    {
                        DebugLogger.Log("BlockEntityGenericContainerPatches.FromTreeAttributesPatch END (not resource crate)");
                        return;
                    }

                    ResourceCrateRuntimeState runtime = ResourceCrateRuntimeState.GetOrCreate(__instance);
                    runtime.LastKnownPos = GetPos(__instance);
                    runtime.ReadFromTree(tree);

                    DebugLogger.Log(
                        $"BlockEntityGenericContainerPatches.FromTreeAttributesPatch.Postfix END | runtime={runtime}"
                    );
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"BlockEntityGenericContainerPatches.FromTreeAttributesPatch.Postfix EXCEPTION | {ex}");
                }
            }
        }

        [HarmonyPatch]
        public static class OnBlockRemovedPatch
        {
            public static MethodBase? TargetMethod()
            {
                DebugLogger.Log("BlockEntityGenericContainerPatches.OnBlockRemovedPatch.TargetMethod START");

                Type? targetType = GetTargetType();
                if (targetType == null)
                {
                    DebugLogger.Error("BlockEntityGenericContainerPatches.OnBlockRemovedPatch.TargetMethod | target type not found");
                    return null;
                }

                MethodInfo? method = AccessTools.Method(targetType, "OnBlockRemoved", Type.EmptyTypes);

                DebugLogger.Log($"BlockEntityGenericContainerPatches.OnBlockRemovedPatch.TargetMethod END -> found={method != null}");
                return method;
            }

            public static void Prefix(object __instance)
            {
                try
                {
                    DebugLogger.Log("BlockEntityGenericContainerPatches.OnBlockRemovedPatch.Prefix START");

                    if (__instance == null)
                    {
                        DebugLogger.Log("BlockEntityGenericContainerPatches.OnBlockRemovedPatch.Prefix END (null instance)");
                        return;
                    }

                    if (!ResourceCrateRuntimeHelpers.IsResourceCrateContainer(__instance))
                    {
                        DebugLogger.Log("BlockEntityGenericContainerPatches.OnBlockRemovedPatch.Prefix END (not resource crate)");
                        return;
                    }

                    if (ResourceCrateRuntimeState.TryGet(__instance, out var runtime) && runtime != null)
                    {
                        UnregisterTickListener(__instance, runtime.TickListenerId);
                        runtime.ResetRuntimeOnly();
                        ResourceCrateRuntimeState.Remove(__instance);

                        DebugLogger.Log(
                            $"BlockEntityGenericContainerPatches.OnBlockRemovedPatch.Prefix END | runtime cleaned"
                        );
                    }
                    else
                    {
                        DebugLogger.Log("BlockEntityGenericContainerPatches.OnBlockRemovedPatch.Prefix END | no runtime found");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"BlockEntityGenericContainerPatches.OnBlockRemovedPatch.Prefix EXCEPTION | {ex}");
                }
            }
        }

        [HarmonyPatch]
        public static class OnBlockUnloadedPatch
        {
            public static MethodBase? TargetMethod()
            {
                DebugLogger.Log("BlockEntityGenericContainerPatches.OnBlockUnloadedPatch.TargetMethod START");

                Type? targetType = GetTargetType();
                if (targetType == null)
                {
                    DebugLogger.Error("BlockEntityGenericContainerPatches.OnBlockUnloadedPatch.TargetMethod | target type not found");
                    return null;
                }

                MethodInfo? method = AccessTools.Method(targetType, "OnBlockUnloaded", Type.EmptyTypes);

                DebugLogger.Log($"BlockEntityGenericContainerPatches.OnBlockUnloadedPatch.TargetMethod END -> found={method != null}");
                return method;
            }

            public static void Prefix(object __instance)
            {
                try
                {
                    DebugLogger.Log("BlockEntityGenericContainerPatches.OnBlockUnloadedPatch.Prefix START");

                    if (__instance == null)
                    {
                        DebugLogger.Log("BlockEntityGenericContainerPatches.OnBlockUnloadedPatch.Prefix END (null instance)");
                        return;
                    }

                    if (!ResourceCrateRuntimeHelpers.IsResourceCrateContainer(__instance))
                    {
                        DebugLogger.Log("BlockEntityGenericContainerPatches.OnBlockUnloadedPatch.Prefix END (not resource crate)");
                        return;
                    }

                    if (ResourceCrateRuntimeState.TryGet(__instance, out var runtime) && runtime != null)
                    {
                        UnregisterTickListener(__instance, runtime.TickListenerId);
                        runtime.ResetRuntimeOnly();

                        DebugLogger.Log(
                            $"BlockEntityGenericContainerPatches.OnBlockUnloadedPatch.Prefix END | runtime reset for reload"
                        );
                    }
                    else
                    {
                        DebugLogger.Log("BlockEntityGenericContainerPatches.OnBlockUnloadedPatch.Prefix END | no runtime found");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"BlockEntityGenericContainerPatches.OnBlockUnloadedPatch.Prefix EXCEPTION | {ex}");
                }
            }
        }
    }
}