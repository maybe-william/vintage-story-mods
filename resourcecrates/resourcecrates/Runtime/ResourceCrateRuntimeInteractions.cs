using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using resourcecrates.Config;
using resourcecrates.Domain;
using resourcecrates.Serialization;
using resourcecrates.Util;

namespace resourcecrates.Runtime
{
    public static class ResourceCrateRuntimeInteractions
    {
        public static bool TryUpgrade(object beInstance, ItemSlot heldSlot)
        {
            try
            {
                DebugLogger.Log(
                    $"ResourceCrateRuntimeInteractions.TryUpgrade START | " +
                    $"beNull={beInstance == null}, heldSlotNull={heldSlot == null}"
                );

                if (beInstance == null || heldSlot?.Itemstack == null)
                {
                    DebugLogger.Log("ResourceCrateRuntimeInteractions.TryUpgrade END -> false (missing be/held item)");
                    return false;
                }

                if (!ResourceCrateRuntimeHelpers.IsResourceCrateContainer(beInstance))
                {
                    DebugLogger.Log("ResourceCrateRuntimeInteractions.TryUpgrade END -> false (not resource crate)");
                    return false;
                }

                ResourceCrateRuntimeState runtime = ResourceCrateRuntimeState.GetOrCreate(beInstance);
                ResourceCrateResolvedConfig config = resourcecratesModSystem.GetResolvedConfigOrThrow();

                int oldTier = runtime.State.CrateTier;
                int targetTier = ResourceCrateRules.GetUpgradeTargetTier(runtime.State, heldSlot.Itemstack, config);

                if (targetTier <= oldTier)
                {
                    DebugLogger.Log("ResourceCrateRuntimeInteractions.TryUpgrade END -> false (target tier not higher)");
                    return false;
                }

                bool shouldConsume = ResourceCrateRules.ShouldConsumeUpgradeItem(runtime.State, heldSlot.Itemstack, config);

                runtime.State.CrateTier = targetTier;

                if (shouldConsume)
                {
                    heldSlot.TakeOut(1);
                    heldSlot.MarkDirty();
                }

                ResourceCrateRuntimeHelpers.MarkDirty(beInstance);

                DebugLogger.Log(
                    $"ResourceCrateRuntimeInteractions.TryUpgrade END -> true | " +
                    $"oldTier={oldTier}, newTier={runtime.State.CrateTier}"
                );
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"ResourceCrateRuntimeInteractions.TryUpgrade EXCEPTION | {ex}");
                return false;
            }
        }

        public static bool TryAssignTarget(object beInstance, ItemSlot heldSlot)
        {
            try
            {
                DebugLogger.Log(
                    $"ResourceCrateRuntimeInteractions.TryAssignTarget START | " +
                    $"beNull={beInstance == null}, heldSlotNull={heldSlot == null}"
                );

                if (beInstance == null || heldSlot?.Itemstack == null)
                {
                    DebugLogger.Log("ResourceCrateRuntimeInteractions.TryAssignTarget END -> false (missing be/held item)");
                    return false;
                }

                if (!ResourceCrateRuntimeHelpers.IsResourceCrateContainer(beInstance))
                {
                    DebugLogger.Log("ResourceCrateRuntimeInteractions.TryAssignTarget END -> false (not resource crate)");
                    return false;
                }

                ResourceCrateRuntimeState runtime = ResourceCrateRuntimeState.GetOrCreate(beInstance);
                ResourceCrateResolvedConfig config = resourcecratesModSystem.GetResolvedConfigOrThrow();
                ICoreAPI? api = ResourceCrateRuntimeHelpers.GetApi(beInstance);

                if (!ResourceCrateRules.CanAssignTarget(runtime.State, heldSlot.Itemstack, config))
                {
                    DebugLogger.Log("ResourceCrateRuntimeInteractions.TryAssignTarget END -> false (rules rejected)");
                    return false;
                }

                runtime.State.TargetItemCode = heldSlot.Itemstack.Collectible.Code;
                runtime.State.ProgressMinutes = 0;
                runtime.State.LastUpdateTotalHours = api?.World?.Calendar?.TotalHours ?? runtime.State.LastUpdateTotalHours;

                if (ResourceCrateRules.ShouldConsumeTargetItem(runtime.State, heldSlot.Itemstack, config))
                {
                    heldSlot.TakeOut(1);
                    heldSlot.MarkDirty();
                }

                ResourceCrateRuntimeHelpers.MarkDirty(beInstance);

                DebugLogger.Log(
                    $"ResourceCrateRuntimeInteractions.TryAssignTarget END -> true | " +
                    $"target={(runtime.State.TargetItemCode == null ? "null" : runtime.State.TargetItemCode.ToShortString())}"
                );
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"ResourceCrateRuntimeInteractions.TryAssignTarget EXCEPTION | {ex}");
                return false;
            }
        }

        public static bool TryReplaceTarget(object beInstance, ItemSlot heldSlot)
        {
            try
            {
                DebugLogger.Log(
                    $"ResourceCrateRuntimeInteractions.TryReplaceTarget START | " +
                    $"beNull={beInstance == null}, heldSlotNull={heldSlot == null}"
                );

                if (beInstance == null || heldSlot?.Itemstack == null)
                {
                    DebugLogger.Log("ResourceCrateRuntimeInteractions.TryReplaceTarget END -> false (missing be/held item)");
                    return false;
                }

                if (!ResourceCrateRuntimeHelpers.IsResourceCrateContainer(beInstance))
                {
                    DebugLogger.Log("ResourceCrateRuntimeInteractions.TryReplaceTarget END -> false (not resource crate)");
                    return false;
                }

                ResourceCrateRuntimeState runtime = ResourceCrateRuntimeState.GetOrCreate(beInstance);
                ResourceCrateResolvedConfig config = resourcecratesModSystem.GetResolvedConfigOrThrow();
                ICoreAPI? api = ResourceCrateRuntimeHelpers.GetApi(beInstance);
                BlockPos? pos = ResourceCrateRuntimeHelpers.GetPos(beInstance);
                InventoryBase? inventory = ResourceCrateRuntimeHelpers.GetInventory(beInstance);
                ItemSlot? outputSlot = inventory == null ? null : ResourceCrateRuntimeHelpers.GetSlot(inventory, 0);

                ItemStack? currentStoredStack = outputSlot?.Itemstack;

                if (!ResourceCrateRules.CanReplaceTarget(runtime.State, heldSlot.Itemstack, currentStoredStack, config))
                {
                    DebugLogger.Log("ResourceCrateRuntimeInteractions.TryReplaceTarget END -> false (rules rejected)");
                    return false;
                }

                if (currentStoredStack != null && currentStoredStack.StackSize > 0 && api?.World != null && pos != null)
                {
                    Vec3d dropPos = pos.ToVec3d().Add(0.5, 0.5, 0.5);
                    ItemStack droppedStack = currentStoredStack.Clone();

                    api.World.SpawnItemEntity(droppedStack, dropPos);

                    outputSlot!.Itemstack = null;
                    outputSlot.MarkDirty();

                    DebugLogger.Log(
                        $"ResourceCrateRuntimeInteractions.TryReplaceTarget | " +
                        $"Dropped previous contents: {droppedStack.Collectible.Code} x{droppedStack.StackSize}"
                    );
                }

                runtime.State.TargetItemCode = heldSlot.Itemstack.Collectible.Code;
                runtime.State.ProgressMinutes = 0;
                runtime.State.LastUpdateTotalHours = api?.World?.Calendar?.TotalHours ?? runtime.State.LastUpdateTotalHours;

                if (ResourceCrateRules.ShouldConsumeTargetItem(runtime.State, heldSlot.Itemstack, config))
                {
                    heldSlot.TakeOut(1);
                    heldSlot.MarkDirty();
                }

                ResourceCrateRuntimeHelpers.MarkDirty(beInstance);

                DebugLogger.Log(
                    $"ResourceCrateRuntimeInteractions.TryReplaceTarget END -> true | " +
                    $"target={(runtime.State.TargetItemCode == null ? "null" : runtime.State.TargetItemCode.ToShortString())}"
                );
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"ResourceCrateRuntimeInteractions.TryReplaceTarget EXCEPTION | {ex}");
                return false;
            }
        }

        public static ItemStack CreateDroppedStack(IWorldAccessor world, Block block, ResourceCrateRuntimeState runtime)
        {
            DebugLogger.Log(
                $"ResourceCrateRuntimeInteractions.CreateDroppedStack START | " +
                $"worldNull={world == null}, blockNull={block == null}, runtimeNull={runtime == null}"
            );

            ItemStack result = new ItemStack(block);

            if (runtime != null && ResourceCrateRules.ShouldPreserveMetadata(runtime.State))
            {
                ResourceCrateStackAttributes.WriteToStack(result, runtime.State);
            }
            else
            {
                ResourceCrateStackAttributes.ClearStackData(result);
            }

            DebugLogger.Log("ResourceCrateRuntimeInteractions.CreateDroppedStack END");
            return result;
        }
    }
}