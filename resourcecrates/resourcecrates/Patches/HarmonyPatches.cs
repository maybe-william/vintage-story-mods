using HarmonyLib;
using resourcecrates.Util;

namespace resourcecrates.Patches
{
    public static class HarmonyPatches
    {
        private const string HarmonyId = "notwilliamresourcecrates";
        private static Harmony? _harmony;
        private static bool _applied;

        public static Harmony HarmonyInstance
        {
            get
            {
                _harmony ??= new Harmony(HarmonyId);
                return _harmony;
            }
        }

        public static void ApplyAll()
        {
            DebugLogger.Log("HarmonyPatches.ApplyAll START");

            if (_applied)
            {
                DebugLogger.Log("HarmonyPatches.ApplyAll END (already applied)");
                return;
            }

            Harmony harmony = HarmonyInstance;

            BlockChutePatches.Apply(harmony);
            BEItemFlowPatches.Apply(harmony);

            _applied = true;

            DebugLogger.Log("HarmonyPatches.ApplyAll END");
        }
    }
}