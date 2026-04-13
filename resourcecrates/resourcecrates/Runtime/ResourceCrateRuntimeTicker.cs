using System;
using Vintagestory.API.Common;
using resourcecrates.Config;
using resourcecrates.Domain;
using resourcecrates.Util;

namespace resourcecrates.Runtime
{
    public static class ResourceCrateRuntimeTicker
    {
        private const double MaxPracticalOverflow = 1000000000;

        public static void OnServerTick(object beInstance, float dt)
        {
            try
            {
                DebugLogger.Log($"ResourceCrateRuntimeTicker.OnServerTick START | dt={dt}");

                if (beInstance == null)
                {
                    DebugLogger.Log("ResourceCrateRuntimeTicker.OnServerTick END (beInstance null)");
                    return;
                }

                if (!ResourceCrateRuntimeHelpers.IsResourceCrateContainer(beInstance))
                {
                    DebugLogger.Log("ResourceCrateRuntimeTicker.OnServerTick END (not resource crate)");
                    return;
                }

                ICoreAPI? api = ResourceCrateRuntimeHelpers.GetApi(beInstance);
                if (api == null || api.Side != EnumAppSide.Server)
                {
                    DebugLogger.Log("ResourceCrateRuntimeTicker.OnServerTick END (api null or not server)");
                    return;
                }

                ResourceCrateRuntimeState runtime = ResourceCrateRuntimeState.GetOrCreate(beInstance);
                runtime.LastKnownPos = ResourceCrateRuntimeHelpers.GetPos(beInstance);

                InventoryBase? inventory = ResourceCrateRuntimeHelpers.GetInventory(beInstance);
                if (inventory == null)
                {
                    DebugLogger.Error("ResourceCrateRuntimeTicker.OnServerTick END (inventory null)");
                    return;
                }

                ItemSlot? outputSlot = ResourceCrateRuntimeHelpers.GetSlot(inventory, 0);
                if (outputSlot == null)
                {
                    DebugLogger.Error("ResourceCrateRuntimeTicker.OnServerTick END (output slot null)");
                    return;
                }

                ResourceCrateResolvedConfig config = resourcecratesModSystem.GetResolvedConfigOrThrow();
                ResourceCrateState state = runtime.State;

                if (!ResourceCrateRules.CanGenerate(state, config))
                {
                    state.LastUpdateTotalHours = GetCurrentTotalHours(api);
                    DebugLogger.Log("ResourceCrateRuntimeTicker.OnServerTick END (cannot generate)");
                    return;
                }

                CollectibleObject? collectible = ResolveTargetCollectible(api, state);
                if (collectible == null)
                {
                    state.LastUpdateTotalHours = GetCurrentTotalHours(api);
                    DebugLogger.Warn("ResourceCrateRuntimeTicker.OnServerTick END (target collectible unresolved)");
                    return;
                }

                double currentTotalHours = GetCurrentTotalHours(api);
                double elapsedHours = currentTotalHours - state.LastUpdateTotalHours;
                if (elapsedHours < 0) elapsedHours = 0;

                double elapsedMinutes = ResourceCrateTierMath.HoursToMinutes(elapsedHours);
                double minutesPerItem = ResourceCrateRules.GetMinutesPerItem(state, config);

                if (minutesPerItem <= 0)
                {
                    state.LastUpdateTotalHours = currentTotalHours;
                    DebugLogger.Log("ResourceCrateRuntimeTicker.OnServerTick END (minutesPerItem invalid)");
                    return;
                }

                var (itemsToProduce, remainingProgress) =
                    ResourceCrateTierMath.ComputeProduction(state.ProgressMinutes, minutesPerItem, elapsedMinutes);

                if (itemsToProduce <= 0)
                {
                    state.ProgressMinutes = remainingProgress;
                    state.LastUpdateTotalHours = currentTotalHours;
                    ResourceCrateRuntimeHelpers.MarkDirty(beInstance);
                    DebugLogger.Log("ResourceCrateRuntimeTicker.OnServerTick END (not enough progress for item)");
                    return;
                }

                string outputBeforeGenerate = DescribeSlot(outputSlot);

                DebugLogger.Log(
                    $"ResourceCrateRuntimeTicker.OnServerTick PRE-INSERT | " +
                    $"side={api.Side}, " +
                    $"inventoryType={inventory.GetType().FullName}, " +
                    $"invHash={inventory.GetHashCode()}, " +
                    $"itemsToProduce={itemsToProduce}, " +
                    $"outputBeforeGenerate={outputBeforeGenerate}"
                );

                int maxStackSize = collectible.MaxStackSize;
                int currentStackSize = 0;

                if (outputSlot.Itemstack != null)
                {
                    ItemStack compareStack = new ItemStack(collectible, 1);

                    if (!outputSlot.Itemstack.Equals(
                        api.World,
                        compareStack,
                        Vintagestory.API.Config.GlobalConstants.IgnoredStackAttributes))
                    {
                        state.LastUpdateTotalHours = currentTotalHours;
                        DebugLogger.Log("ResourceCrateRuntimeTicker.OnServerTick END (output slot contains different item)");
                        return;
                    }

                    currentStackSize = outputSlot.Itemstack.StackSize;
                }

                int remainingRoom = maxStackSize - currentStackSize;
                double overflow;

                if (remainingRoom <= 0)
                {
                    overflow = remainingProgress + (itemsToProduce * minutesPerItem);
                    state.ProgressMinutes = Math.Min(MaxPracticalOverflow, overflow);
                    state.LastUpdateTotalHours = currentTotalHours;

                    ResourceCrateRuntimeHelpers.MarkDirty(beInstance);

                    DebugLogger.Log(
                        $"ResourceCrateRuntimeTicker.OnServerTick END (output slot full, banked stockpile) | " +
                        $"itemsToProduce={itemsToProduce}, " +
                        $"newProgress={state.ProgressMinutes}"
                    );
                    return;
                }

                int actualToProduce = itemsToProduce <= remainingRoom ? itemsToProduce : remainingRoom;

                if (outputSlot.Itemstack == null)
                {
                    outputSlot.Itemstack = new ItemStack(collectible, actualToProduce);
                }
                else
                {
                    outputSlot.Itemstack.StackSize += actualToProduce;
                }

                outputSlot.MarkDirty();

                int uninserted = itemsToProduce - actualToProduce;
                overflow = remainingProgress + (uninserted * minutesPerItem);

                state.ProgressMinutes = Math.Min(MaxPracticalOverflow, overflow);
                state.LastUpdateTotalHours = currentTotalHours;

                string outputAfterGenerate = DescribeSlot(outputSlot);

                ResourceCrateRuntimeHelpers.MarkDirty(beInstance);

                DebugLogger.Log(
                    $"ResourceCrateRuntimeTicker.OnServerTick POST-INSERT | " +
                    $"actualToProduce={actualToProduce}, " +
                    $"uninserted={uninserted}, " +
                    $"outputAfterGenerate={outputAfterGenerate}, " +
                    $"state={state}"
                );
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"ResourceCrateRuntimeTicker.OnServerTick EXCEPTION | {ex}");
            }
        }

        private static double GetCurrentTotalHours(ICoreAPI api)
        {
            DebugLogger.Log("ResourceCrateRuntimeTicker.GetCurrentTotalHours START");

            double result = api?.World?.Calendar?.TotalHours ?? 0;

            DebugLogger.Log($"ResourceCrateRuntimeTicker.GetCurrentTotalHours END -> {result}");
            return result;
        }

        private static CollectibleObject? ResolveTargetCollectible(ICoreAPI api, ResourceCrateState state)
        {
            DebugLogger.Log("ResourceCrateRuntimeTicker.ResolveTargetCollectible START");

            if (api?.World == null || state.TargetItemCode == null)
            {
                DebugLogger.Log("ResourceCrateRuntimeTicker.ResolveTargetCollectible END -> null (api/world/target missing)");
                return null;
            }

            CollectibleObject? result =
                (CollectibleObject?)api.World.GetItem(state.TargetItemCode)
                ?? api.World.GetBlock(state.TargetItemCode);

            DebugLogger.Log(
                $"ResourceCrateRuntimeTicker.ResolveTargetCollectible END -> " +
                $"{(result == null ? "null" : result.Code.ToString())}"
            );

            return result;
        }

        private static string DescribeSlot(ItemSlot slot)
        {
            if (slot?.Itemstack == null)
            {
                return "empty";
            }

            return slot.Itemstack.Collectible.Code + " x" + slot.Itemstack.StackSize;
        }
    }
}