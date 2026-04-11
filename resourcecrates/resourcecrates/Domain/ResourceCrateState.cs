using Vintagestory.API.Common;
using resourcecrates.Util;

namespace resourcecrates.Domain
{
    public class ResourceCrateState
    {
        public int CrateTier { get; set; } = 0;
        public AssetLocation? TargetItemCode { get; set; } = null;
        public double ProgressMinutes { get; set; } = 0;
        public double LastUpdateTotalHours { get; set; } = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        public ResourceCrateState()
        {
            DebugLogger.Log("ResourceCrateState.ctor START");

            // Defaults already set via property initializers

            DebugLogger.Log("ResourceCrateState.ctor END");
        }

        /// <summary>
        /// Returns true if the crate has been assigned a generation target.
        /// </summary>
        public bool HasTargetItem
        {
            get
            {
                DebugLogger.Log("ResourceCrateState.HasTargetItem START");

                bool result = TargetItemCode != null;

                DebugLogger.Log($"ResourceCrateState.HasTargetItem END -> {result}");
                return result;
            }
        }

        /// <summary>
        /// Returns true if this crate still has default/no meaningful metadata.
        /// </summary>
        public bool IsUninitialized
        {
            get
            {
                DebugLogger.Log("ResourceCrateState.IsUninitialized START");

                bool result =
                    CrateTier == 0 &&
                    TargetItemCode == null &&
                    ProgressMinutes <= 0;

                DebugLogger.Log($"ResourceCrateState.IsUninitialized END -> {result}");
                return result;
            }
        }

        /// <summary>
        /// Clears target assignment and generation progress,
        /// but does not reset crate tier.
        /// </summary>
        public void ClearTarget()
        {
            DebugLogger.Log($"ResourceCrateState.ClearTarget START | Before: {this}");

            TargetItemCode = null;
            ProgressMinutes = 0;

            DebugLogger.Log($"ResourceCrateState.ClearTarget END | After: {this}");
        }

        /// <summary>
        /// Fully resets the crate back to a fresh base-state crate.
        /// </summary>
        public void Reset()
        {
            DebugLogger.Log($"ResourceCrateState.Reset START | Before: {this}");

            CrateTier = 0;
            TargetItemCode = null;
            ProgressMinutes = 0;
            LastUpdateTotalHours = 0;

            DebugLogger.Log($"ResourceCrateState.Reset END | After: {this}");
        }

        /// <summary>
        /// Creates a shallow copy of this state object.
        /// </summary>
        public ResourceCrateState Clone()
        {
            DebugLogger.Log($"ResourceCrateState.Clone START | Source: {this}");

            var clone = new ResourceCrateState
            {
                CrateTier = CrateTier,
                TargetItemCode = TargetItemCode == null ? null : TargetItemCode.Clone(),
                ProgressMinutes = ProgressMinutes,
                LastUpdateTotalHours = LastUpdateTotalHours
            };

            DebugLogger.Log($"ResourceCrateState.Clone END | Clone: {clone}");
            return clone;
        }

        public override string ToString()
        {
            DebugLogger.Log("ResourceCrateState.ToString START");

            string result = $"Tier={CrateTier}, Target={(TargetItemCode?.ToString() ?? "null")}, Progress={ProgressMinutes:0.###}, LastHours={LastUpdateTotalHours:0.###}";

            DebugLogger.Log($"ResourceCrateState.ToString END -> {result}");
            return result;
        }
    }
}