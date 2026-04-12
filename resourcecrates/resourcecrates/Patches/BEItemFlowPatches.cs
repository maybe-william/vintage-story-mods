using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using resourcecrates.BlockEntities;
using resourcecrates.Util;

namespace resourcecrates.Patches
{
    public static class BEItemFlowPatches
    {
        public static void Apply(Harmony harmony)
        {
            DebugLogger.Log("BEItemFlowPatches.Apply START");

            harmony.CreateClassProcessor(typeof(TryPullFromPatch)).Patch();
            harmony.CreateClassProcessor(typeof(TryPushIntoPatch)).Patch();

            DebugLogger.Log("BEItemFlowPatches.Apply END");
        }

        [HarmonyPatch]
        public static class TryPullFromPatch
        {
            public static MethodBase TargetMethod()
            {
                DebugLogger.Log("BEItemFlowPatches.TryPullFromPatch.TargetMethod START");

                Type beItemFlowType = AccessTools.TypeByName("Vintagestory.GameContent.BlockEntityItemFlow");
                if (beItemFlowType == null)
                {
                    DebugLogger.Error("BEItemFlowPatches.TryPullFromPatch.TargetMethod | Could not find BlockEntityItemFlow");
                    return null;
                }

                MethodInfo method = AccessTools.Method(beItemFlowType, "TryPullFrom", new[] { typeof(BlockFacing) });

                DebugLogger.Log($"BEItemFlowPatches.TryPullFromPatch.TargetMethod END -> found={method != null}");
                return method;
            }

            public static bool Prefix(object __instance, BlockFacing inputFace)
            {
                try
                {
                    if (__instance == null || inputFace == null)
                    {
                        return true;
                    }

                    ICoreAPI api = AccessTools.Property(__instance.GetType(), "Api")?.GetValue(__instance) as ICoreAPI;
                    BlockPos pos = AccessTools.Property(__instance.GetType(), "Pos")?.GetValue(__instance) as BlockPos;

                    if (api?.World?.BlockAccessor == null || pos == null)
                    {
                        return true;
                    }

                    IBlockAccessor ba = api.World.BlockAccessor;
                    BlockPos inputPosition = pos.AddCopy(inputFace);

                    if (ba.GetBlockEntity(inputPosition) is not BlockEntityResourceCrate crateBe)
                    {
                        return true;
                    }

                    DebugLogger.Log(
                        $"BEItemFlowPatches.TryPullFromPatch.Prefix START | " +
                        $"inputFace={inputFace.Code}, inputPosition={inputPosition}"
                    );

                    InventoryBase flowInventory = GetFlowInventory(__instance);
                    if (flowInventory == null)
                    {
                        DebugLogger.Error("BEItemFlowPatches.TryPullFromPatch.Prefix | flowInventory was null");
                        return false;
                    }

                    var method = AccessTools.Method(crateBe.Inventory.GetType(), "GetAutoPullFromSlot");
                    ItemSlot sourceSlot = method?.Invoke(crateBe.Inventory, new object[] { inputFace.Opposite }) as ItemSlot;
                    ItemSlot targetSlot = sourceSlot == null ? null : flowInventory.GetBestSuitedSlot(sourceSlot).slot;

                    if (sourceSlot == null || targetSlot == null)
                    {
                        DebugLogger.Log("BEItemFlowPatches.TryPullFromPatch.Prefix END (no source or target slot)");
                        return false;
                    }

                    if (sourceSlot.Itemstack == null || sourceSlot.Itemstack.StackSize <= 0)
                    {
                        DebugLogger.Log("BEItemFlowPatches.TryPullFromPatch.Prefix END (source empty)");
                        return false;
                    }

                    float itemFlowAccum = GetItemFlowAccum(__instance);
                    if (itemFlowAccum < 1f)
                    {
                        DebugLogger.Log($"BEItemFlowPatches.TryPullFromPatch.Prefix END (itemFlowAccum < 1) | itemFlowAccum={itemFlowAccum}");
                        return false;
                    }

                    int maxHorizontalTravel = GetMaxHorizontalTravel(__instance);
                    int horTravelled = sourceSlot.Itemstack.Attributes.GetInt("chuteQHTravelled");

                    if (horTravelled >= maxHorizontalTravel)
                    {
                        DebugLogger.Log(
                            $"BEItemFlowPatches.TryPullFromPatch.Prefix END (max horizontal travel reached) | " +
                            $"horTravelled={horTravelled}, maxHorizontalTravel={maxHorizontalTravel}"
                        );
                        return false;
                    }

                    ItemStackMoveOperation op = new ItemStackMoveOperation(
                        api.World,
                        EnumMouseButton.Left,
                        0,
                        EnumMergePriority.DirectMerge,
                        (int)itemFlowAccum
                    );

                    int moved = sourceSlot.TryPutInto(targetSlot, ref op);

                    if (moved > 0)
                    {
                        targetSlot.Itemstack?.Attributes.SetInt(
                            "chuteQHTravelled",
                            inputFace.IsHorizontal ? (horTravelled + 1) : 0
                        );
                        targetSlot.Itemstack?.Attributes.SetInt("chuteDir", inputFace.Opposite.Index);

                        sourceSlot.MarkDirty();
                        targetSlot.MarkDirty();

                        MarkDirty(__instance, false);
                        crateBe.MarkDirty();

                        if (api.World.Rand.NextDouble() < 0.2)
                        {
                            object tumbleSound = AccessTools.Field(__instance.GetType(), "TumbleSound")?.GetValue(__instance);

                            if (tumbleSound != null)
                            {
                                api.World.PlaySoundAt((dynamic)tumbleSound, pos, 0, null);
                            }
                        }

                        SetItemFlowAccum(__instance, itemFlowAccum - moved);

                        DebugLogger.Log(
                            $"BEItemFlowPatches.TryPullFromPatch.Prefix END (moved) | " +
                            $"moved={moved}, inputFace={inputFace.Code}, inputPosition={inputPosition}"
                        );
                    }
                    else
                    {
                        DebugLogger.Log("BEItemFlowPatches.TryPullFromPatch.Prefix END (moved 0)");
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"BEItemFlowPatches.TryPullFromPatch.Prefix EXCEPTION | {ex}");
                    return true;
                }
            }
        }

        [HarmonyPatch]
        public static class TryPushIntoPatch
        {
            public static MethodBase TargetMethod()
            {
                DebugLogger.Log("BEItemFlowPatches.TryPushIntoPatch.TargetMethod START");

                Type beItemFlowType = AccessTools.TypeByName("Vintagestory.GameContent.BlockEntityItemFlow");
                if (beItemFlowType == null)
                {
                    DebugLogger.Error("BEItemFlowPatches.TryPushIntoPatch.TargetMethod | Could not find BlockEntityItemFlow");
                    return null;
                }

                MethodInfo method = AccessTools.Method(beItemFlowType, "TryPushInto", new[] { typeof(BlockFacing) });

                DebugLogger.Log($"BEItemFlowPatches.TryPushIntoPatch.TargetMethod END -> found={method != null}");
                return method;
            }

            public static bool Prefix(object __instance, BlockFacing outputFace, ref bool __result)
            {
                try
                {
                    if (__instance == null || outputFace == null)
                    {
                        return true;
                    }

                    ICoreAPI api = AccessTools.Property(__instance.GetType(), "Api")?.GetValue(__instance) as ICoreAPI;
                    BlockPos pos = AccessTools.Property(__instance.GetType(), "Pos")?.GetValue(__instance) as BlockPos;

                    if (api?.World?.BlockAccessor == null || pos == null)
                    {
                        return true;
                    }

                    BlockPos outputPosition = pos.AddCopy(outputFace);
                    IBlockAccessor ba = api.World.BlockAccessor;

                    if (ba.GetBlockEntity(outputPosition) is not BlockEntityResourceCrate)
                    {
                        return true;
                    }

                    DebugLogger.Log(
                        $"BEItemFlowPatches.TryPushIntoPatch.Prefix | " +
                        $"blocked push into resource crate at {outputPosition} from face {outputFace.Code}"
                    );

                    __result = false;
                    return false;
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"BEItemFlowPatches.TryPushIntoPatch.Prefix EXCEPTION | {ex}");
                    return true;
                }
            }
        }

        private static InventoryBase GetFlowInventory(object beItemFlowInstance)
        {
            return AccessTools.Property(beItemFlowInstance.GetType(), "Inventory")?.GetValue(beItemFlowInstance) as InventoryBase;
        }

        private static float GetItemFlowAccum(object beItemFlowInstance)
        {
            object value = AccessTools.Field(beItemFlowInstance.GetType(), "itemFlowAccum")?.GetValue(beItemFlowInstance);
            return value is float f ? f : 0f;
        }

        private static void SetItemFlowAccum(object beItemFlowInstance, float value)
        {
            AccessTools.Field(beItemFlowInstance.GetType(), "itemFlowAccum")?.SetValue(beItemFlowInstance, value);
        }

        private static int GetMaxHorizontalTravel(object beItemFlowInstance)
        {
            object value = AccessTools.Field(beItemFlowInstance.GetType(), "MaxHorizontalTravel")?.GetValue(beItemFlowInstance);
            return value is int i ? i : 0;
        }

        private static void MarkDirty(object beItemFlowInstance, bool redrawOnClient)
        {
            MethodInfo method = AccessTools.Method(beItemFlowInstance.GetType(), "MarkDirty", new[] { typeof(bool) });
            method?.Invoke(beItemFlowInstance, new object[] { redrawOnClient });
        }
    }
}