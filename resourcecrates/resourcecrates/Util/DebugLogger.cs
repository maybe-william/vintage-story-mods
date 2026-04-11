using Vintagestory.API.Common;

namespace resourcecrates.Util
{
    public static class DebugLogger
    {
        // Flip this to enable/disable debug logging
        public const bool DebugEnabled = true;

        private static ILogger logger;

        public static void Init(ICoreAPI api)
        {
            logger = api.Logger;
        }

        public static void Log(string message)
        {
            if (!DebugEnabled || logger == null) return;

            logger.Notification($"[ResourceCrates][DEBUG] {message}");
        }

        public static void Warn(string message)
        {
            if (!DebugEnabled || logger == null) return;

            logger.Warning($"[ResourceCrates][DEBUG] {message}");
        }

        public static void Error(string message)
        {
            // Errors should probably ALWAYS log, regardless of debug flag
            if (logger == null) return;

            logger.Error($"[ResourceCrates][ERROR] {message}");
        }
    }
}