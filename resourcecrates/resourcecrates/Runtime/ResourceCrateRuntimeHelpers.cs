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
            if (beInstance == null) return false;

            object? blockObj = AccessTools.Property(beInstance.GetType(), "Block")?.GetValue(beInstance);
            if (blockObj == null) return false;

            object? codeObj = AccessTools.Property(blockObj.GetType(), "Code")?.GetValue(blockObj);
            if (codeObj == null) return false;

            string codeString = codeObj.ToString() ?? "";

            bool result =
                codeString.Equals("notwilliamresourcecrates:resourcecrate", StringComparison.OrdinalIgnoreCase) ||
                codeString.EndsWith(":resourcecrate", StringComparison.OrdinalIgnoreCase);

            DebugLogger.Log(
                $"ResourceCrateRuntimeHelpers.IsResourceCrateContainer | " +
                $"code={codeString}, result={result}"
            );

            return result;
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