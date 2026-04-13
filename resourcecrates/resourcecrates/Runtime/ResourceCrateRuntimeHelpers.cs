using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using resourcecrates.Util;

namespace resourcecrates.Runtime
{
    public static class ResourceCrateRuntimeHelpers
    {

        public static bool IsResourceCrateContainer(object beInstance)
        {
            DebugLogger.Log("IsResourceCrateContainer START");

            if (beInstance == null)
            {
                DebugLogger.Log("IsResourceCrateContainer -> false (beInstance null)");
                return false;
            }

            try
            {
                var blockProp = AccessTools.Property(beInstance.GetType(), "Block");
                var blockObj = blockProp?.GetValue(beInstance);

                if (blockObj is Block block)
                {
                    string codeString = block.Code?.ToString() ?? "NULL";
                    DebugLogger.Log($"Block TYPE: {block.GetType().FullName}");
                    DebugLogger.Log($"Block Code (ToString): {codeString}");

                    bool result =
                        codeString.Equals("notwilliamresourcecrates:resourcecrate", StringComparison.OrdinalIgnoreCase) ||
                        codeString.EndsWith(":resourcecrate", StringComparison.OrdinalIgnoreCase);

                    DebugLogger.Log($"IsResourceCrateContainer RESULT (block path): {result}");
                    return result;
                }

                ICoreAPI? api = GetApi(beInstance);
                BlockPos? pos = GetPos(beInstance);

                DebugLogger.Log($"Fallback API null: {api == null}");
                DebugLogger.Log($"Fallback POS null: {pos == null}");

                if (api?.World != null && pos != null)
                {
                    Block worldBlock = api.World.BlockAccessor.GetBlock(pos);
                    string codeString = worldBlock?.Code?.ToString() ?? "NULL";

                    DebugLogger.Log($"Fallback Block TYPE: {worldBlock?.GetType().FullName}");
                    DebugLogger.Log($"Fallback Block Code: {codeString}");

                    bool result =
                        codeString.Equals("notwilliamresourcecrates:resourcecrate", StringComparison.OrdinalIgnoreCase) ||
                        codeString.EndsWith(":resourcecrate", StringComparison.OrdinalIgnoreCase);

                    DebugLogger.Log($"IsResourceCrateContainer RESULT (fallback path): {result}");
                    return result;
                }

                DebugLogger.Log("IsResourceCrateContainer -> false (no valid detection path)");
                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"IsResourceCrateContainer EXCEPTION: {ex}");
                return false;
            }
        }

        public static ICoreAPI? GetApi(object beInstance)
        {
            return AccessTools.Property(beInstance.GetType(), "Api")?.GetValue(beInstance) as ICoreAPI;
        }

        public static BlockPos? GetPos(object beInstance)
        {
            return AccessTools.Property(beInstance.GetType(), "Pos")?.GetValue(beInstance) as BlockPos;
        }

        public static InventoryBase? GetInventory(object beInstance)
        {
            return AccessTools.Property(beInstance.GetType(), "Inventory")?.GetValue(beInstance) as InventoryBase;
        }

        public static ItemSlot? GetSlot(InventoryBase inventory, int index)
        {
            try
            {
                DebugLogger.Log($"ResourceCrateRuntimeHelpers.GetSlot START | index={index}");

                ItemSlot? result = inventory[index];

                DebugLogger.Log($"ResourceCrateRuntimeHelpers.GetSlot END -> {(result == null ? "null" : "found")}");
                return result;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"ResourceCrateRuntimeHelpers.GetSlot EXCEPTION | {ex}");
                return null;
            }
        }

        public static void MarkDirty(object beInstance)
        {
            try
            {
                DebugLogger.Log("ResourceCrateRuntimeHelpers.MarkDirty START");

                MethodInfo? method = AccessTools.Method(beInstance.GetType(), "MarkDirty", new[] { typeof(bool) });

                if (method != null)
                {
                    method.Invoke(beInstance, new object[] { false });
                    DebugLogger.Log("ResourceCrateRuntimeHelpers.MarkDirty END (bool overload)");
                    return;
                }

                method = AccessTools.Method(beInstance.GetType(), "MarkDirty", Type.EmptyTypes);
                if (method != null)
                {
                    method.Invoke(beInstance, Array.Empty<object>());
                    DebugLogger.Log("ResourceCrateRuntimeHelpers.MarkDirty END (parameterless overload)");
                    return;
                }

                DebugLogger.Log("ResourceCrateRuntimeHelpers.MarkDirty END (no method found)");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"ResourceCrateRuntimeHelpers.MarkDirty EXCEPTION | {ex}");
            }
        }
    }
}