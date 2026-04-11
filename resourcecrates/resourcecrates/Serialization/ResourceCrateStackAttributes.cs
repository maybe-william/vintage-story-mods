using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using resourcecrates.Domain;
using resourcecrates.Util;

namespace resourcecrates.Serialization
{
    public static class ResourceCrateStackAttributes
    {
        public const string RootAttributeKey = "resourceCrateData";
        public const string CrateTierKey = "crateTier";
        public const string TargetItemCodeKey = "targetItemCode";
        public const string ProgressMinutesKey = "progressMinutes";
        public const string LastUpdateTotalHoursKey = "lastUpdateTotalHours";

        public static bool HasMeaningfulData(ItemStack? stack)
        {
            DebugLogger.Log("ResourceCrateStackAttributes.HasMeaningfulData START");

            bool result = false;

            if (stack?.Attributes != null)
            {
                ITreeAttribute? tree = stack.Attributes[RootAttributeKey] as ITreeAttribute;
                result = tree != null;
            }

            DebugLogger.Log($"ResourceCrateStackAttributes.HasMeaningfulData END -> {result}");
            return result;
        }

        public static void WriteToStack(ItemStack? stack, ResourceCrateState? state)
        {
            DebugLogger.Log($"ResourceCrateStackAttributes.WriteToStack START | stackNull={stack == null}, state={state}");

            if (stack == null)
            {
                DebugLogger.Log("ResourceCrateStackAttributes.WriteToStack END | stack was null, no action taken");
                return;
            }

            if (state == null || state.IsUninitialized)
            {
                ClearStackData(stack);
                DebugLogger.Log("ResourceCrateStackAttributes.WriteToStack END | state null or uninitialized, cleared stack data");
                return;
            }

            stack.Attributes ??= new TreeAttribute();

            TreeAttribute rootTree = new TreeAttribute();
            rootTree.SetInt(CrateTierKey, state.CrateTier);
            rootTree.SetDouble(ProgressMinutesKey, state.ProgressMinutes);
            rootTree.SetDouble(LastUpdateTotalHoursKey, state.LastUpdateTotalHours);

            if (state.TargetItemCode != null)
            {
                rootTree.SetString(TargetItemCodeKey, state.TargetItemCode.ToShortString());
            }

            stack.Attributes[RootAttributeKey] = rootTree;

            DebugLogger.Log("ResourceCrateStackAttributes.WriteToStack END | wrote resource crate metadata to stack");
        }

        public static bool TryReadFromStack(ItemStack? stack, out ResourceCrateState state)
        {
            DebugLogger.Log("ResourceCrateStackAttributes.TryReadFromStack START");

            state = new ResourceCrateState();

            if (stack?.Attributes == null)
            {
                DebugLogger.Log("ResourceCrateStackAttributes.TryReadFromStack END -> false (stack or attributes null)");
                return false;
            }

            ITreeAttribute? rootTree = stack.Attributes[RootAttributeKey] as ITreeAttribute;
            if (rootTree == null)
            {
                DebugLogger.Log("ResourceCrateStackAttributes.TryReadFromStack END -> false (no root tree)");
                return false;
            }

            state.CrateTier = rootTree.GetInt(CrateTierKey);
            state.ProgressMinutes = rootTree.GetDouble(ProgressMinutesKey);
            state.LastUpdateTotalHours = rootTree.GetDouble(LastUpdateTotalHoursKey);

            string? targetItemCode = rootTree.GetString(TargetItemCodeKey, null);
            if (!string.IsNullOrWhiteSpace(targetItemCode))
            {
                state.TargetItemCode = new AssetLocation(targetItemCode);
            }

            DebugLogger.Log($"ResourceCrateStackAttributes.TryReadFromStack END -> true | state={state}");
            return true;
        }

        public static void ClearStackData(ItemStack? stack)
        {
            DebugLogger.Log("ResourceCrateStackAttributes.ClearStackData START");

            if (stack?.Attributes == null)
            {
                DebugLogger.Log("ResourceCrateStackAttributes.ClearStackData END | stack or attributes null, no action taken");
                return;
            }

            stack.Attributes.RemoveAttribute(RootAttributeKey);

            if (stack.Attributes.Count == 0)
            {
                stack.Attributes = null;
            }

            DebugLogger.Log("ResourceCrateStackAttributes.ClearStackData END");
        }

        public static ResourceCrateState CreateStateFromStackOrDefault(ItemStack? stack)
        {
            DebugLogger.Log("ResourceCrateStackAttributes.CreateStateFromStackOrDefault START");

            ResourceCrateState result;

            if (TryReadFromStack(stack, out ResourceCrateState state))
            {
                result = state;
            }
            else
            {
                result = new ResourceCrateState();
            }

            DebugLogger.Log($"ResourceCrateStackAttributes.CreateStateFromStackOrDefault END -> {result}");
            return result;
        }
    }
}