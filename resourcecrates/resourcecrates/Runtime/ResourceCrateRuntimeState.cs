using System;
using System.Runtime.CompilerServices;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using resourcecrates.Domain;
using resourcecrates.Serialization;
using resourcecrates.Util;

namespace resourcecrates.Runtime
{
    /// <summary>
    /// External runtime state attached 1:1 to a BlockEntityGenericContainer instance
    /// when that BE is acting as a resource crate.
    ///
    /// Runtime identity is keyed by the BE object instance via ConditionalWeakTable.
    /// Persistent identity/state is stored in the BE's tree attributes.
    /// </summary>
    public sealed class ResourceCrateRuntimeState
    {
        public const string TreeStateKey = "resourceCrateState";
        public const string PersistentIdKey = "resourceCratePersistentId";

        private static readonly ConditionalWeakTable<object, ResourceCrateRuntimeState> RuntimeByBe = new();

        /// <summary>
        /// Main gameplay state for the crate.
        /// </summary>
        public ResourceCrateState State { get; private set; } = new ResourceCrateState();

        /// <summary>
        /// Registered server tick listener id for this live BE instance.
        /// -1 means no active listener.
        /// </summary>
        public long TickListenerId { get; set; } = -1;

        /// <summary>
        /// Whether Initialize-side setup has already been performed for the current live BE instance.
        /// </summary>
        public bool IsInitialized { get; set; }

        /// <summary>
        /// Optional persistent identifier for this crate across unload/reload.
        /// Not strictly needed for V1, but cheap to include now.
        /// </summary>
        public string? PersistentId { get; private set; }

        /// <summary>
        /// Helpful for logging/debugging only.
        /// </summary>
        public BlockPos? LastKnownPos { get; set; }

        private ResourceCrateRuntimeState()
        {
            DebugLogger.Log("ResourceCrateRuntimeState.ctor START");

            State = new ResourceCrateState();
            TickListenerId = -1;
            IsInitialized = false;
            PersistentId = null;
            LastKnownPos = null;

            DebugLogger.Log("ResourceCrateRuntimeState.ctor END");
        }

        public static ResourceCrateRuntimeState GetOrCreate(object beInstance)
        {
            if (beInstance == null) throw new ArgumentNullException(nameof(beInstance));

            DebugLogger.Log("ResourceCrateRuntimeState.GetOrCreate START");

            ResourceCrateRuntimeState result = RuntimeByBe.GetValue(
                beInstance,
                _ => new ResourceCrateRuntimeState()
            );

            DebugLogger.Log(
                $"ResourceCrateRuntimeState.GetOrCreate END | " +
                $"runtimeHash={result.GetHashCode()}, " +
                $"beHash={beInstance.GetHashCode()}"
            );

            return result;
        }

        public static bool TryGet(object beInstance, out ResourceCrateRuntimeState? runtime)
        {
            if (beInstance == null)
            {
                runtime = null;
                return false;
            }

            bool found = RuntimeByBe.TryGetValue(beInstance, out ResourceCrateRuntimeState existing);
            runtime = existing;

            DebugLogger.Log(
                $"ResourceCrateRuntimeState.TryGet | " +
                $"found={found}, " +
                $"beHash={beInstance.GetHashCode()}, " +
                $"runtimeHash={(existing == null ? "null" : existing.GetHashCode().ToString())}"
            );

            return found;
        }

        public static void Remove(object beInstance)
        {
            if (beInstance == null) return;

            DebugLogger.Log($"ResourceCrateRuntimeState.Remove START | beHash={beInstance.GetHashCode()}");

            RuntimeByBe.Remove(beInstance);

            DebugLogger.Log("ResourceCrateRuntimeState.Remove END");
        }

        public void ResetRuntimeOnly()
        {
            DebugLogger.Log("ResourceCrateRuntimeState.ResetRuntimeOnly START");

            TickListenerId = -1;
            IsInitialized = false;
            LastKnownPos = null;

            DebugLogger.Log("ResourceCrateRuntimeState.ResetRuntimeOnly END");
        }

        public string EnsurePersistentId()
        {
            DebugLogger.Log("ResourceCrateRuntimeState.EnsurePersistentId START");

            if (string.IsNullOrWhiteSpace(PersistentId))
            {
                PersistentId = Guid.NewGuid().ToString("N");
                DebugLogger.Log($"ResourceCrateRuntimeState.EnsurePersistentId | created={PersistentId}");
            }

            DebugLogger.Log($"ResourceCrateRuntimeState.EnsurePersistentId END -> {PersistentId}");
            return PersistentId;
        }

        public void SetPersistentId(string? persistentId)
        {
            DebugLogger.Log(
                $"ResourceCrateRuntimeState.SetPersistentId START | " +
                $"incoming={(string.IsNullOrWhiteSpace(persistentId) ? "null/empty" : persistentId)}"
            );

            PersistentId = string.IsNullOrWhiteSpace(persistentId) ? null : persistentId;

            DebugLogger.Log(
                $"ResourceCrateRuntimeState.SetPersistentId END -> " +
                $"{(PersistentId ?? "null")}"
            );
        }

        public void SetState(ResourceCrateState state)
        {
            DebugLogger.Log($"ResourceCrateRuntimeState.SetState START | incomingNull={state == null}");

            State = state ?? new ResourceCrateState();

            DebugLogger.Log($"ResourceCrateRuntimeState.SetState END | state={State}");
        }

        public void WriteToTree(ITreeAttribute tree)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));

            DebugLogger.Log("ResourceCrateRuntimeState.WriteToTree START");

            TreeAttribute stateTree = new TreeAttribute();
            stateTree.SetInt(ResourceCrateStackAttributes.CrateTierKey, State.CrateTier);
            stateTree.SetDouble(ResourceCrateStackAttributes.ProgressMinutesKey, State.ProgressMinutes);
            stateTree.SetDouble(ResourceCrateStackAttributes.LastUpdateTotalHoursKey, State.LastUpdateTotalHours);

            if (State.TargetItemCode != null)
            {
                stateTree.SetString(
                    ResourceCrateStackAttributes.TargetItemCodeKey,
                    State.TargetItemCode.ToShortString()
                );
            }

            tree[TreeStateKey] = stateTree;

            if (!string.IsNullOrWhiteSpace(PersistentId))
            {
                tree.SetString(PersistentIdKey, PersistentId);
            }

            DebugLogger.Log(
                $"ResourceCrateRuntimeState.WriteToTree END | " +
                $"persistentId={(PersistentId ?? "null")}, " +
                $"state={State}"
            );
        }

        public void ReadFromTree(ITreeAttribute tree)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));

            DebugLogger.Log("ResourceCrateRuntimeState.ReadFromTree START");

            string persistentId = tree.GetString(PersistentIdKey, null);
            SetPersistentId(persistentId);

            ITreeAttribute? stateTree = tree[TreeStateKey] as ITreeAttribute;
            if (stateTree != null)
            {
                ResourceCrateState restored = new ResourceCrateState
                {
                    CrateTier = stateTree.GetInt(ResourceCrateStackAttributes.CrateTierKey),
                    ProgressMinutes = stateTree.GetDouble(ResourceCrateStackAttributes.ProgressMinutesKey),
                    LastUpdateTotalHours = stateTree.GetDouble(ResourceCrateStackAttributes.LastUpdateTotalHoursKey)
                };

                string targetCode = stateTree.GetString(ResourceCrateStackAttributes.TargetItemCodeKey, "");
                restored.TargetItemCode = string.IsNullOrWhiteSpace(targetCode)
                    ? null
                    : new AssetLocation(targetCode);

                State = restored;

                DebugLogger.Log(
                    $"ResourceCrateRuntimeState.ReadFromTree | restored state | " +
                    $"persistentId={(PersistentId ?? "null")}, state={State}"
                );
            }
            else
            {
                State = new ResourceCrateState();
                DebugLogger.Log("ResourceCrateRuntimeState.ReadFromTree | no state tree found, reset to default");
            }

            DebugLogger.Log("ResourceCrateRuntimeState.ReadFromTree END");
        }

        public override string ToString()
        {
            return
                $"PersistentId={(PersistentId ?? "null")}, " +
                $"TickListenerId={TickListenerId}, " +
                $"IsInitialized={IsInitialized}, " +
                $"LastKnownPos={(LastKnownPos == null ? "null" : LastKnownPos.ToString())}, " +
                $"State={State}";
        }
    }
}