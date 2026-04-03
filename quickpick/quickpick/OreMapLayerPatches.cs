using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace quickpick
{
    public static class OreMapLayerPatches
    {
        private const string QuickPickGuidPrefix = "quickpick:";

        private static ICoreAPI api;

        private static Type propickReadingType;
        private static Type oreMapComponentType;
        private static Type modSystemOreMapType;

        private static MethodInfo getModSystemGenericMethod;
        private static MethodInfo genProbeResultsMethod;
        private static MethodInfo toHumanReadableMethod;
        private static MethodInfo didProbeMethod;

        private static ConstructorInfo oreMapComponentCtor;

        private static FieldInfo ppwsField;
        private static FieldInfo pageCodesField;

        private static FieldInfo oreReadingsField;
        private static FieldInfo mentionThresholdField;
        private static FieldInfo oreReadingTotalFactorField;
        private static FieldInfo oreMapComponentColorField;

        private static PropertyInfo readingGuidProperty;

        public static void RegisterPatches(Harmony harmony, ICoreAPI coreApi, Type propickType)
        {
            api = coreApi;

            propickReadingType = AccessTools.TypeByName("Vintagestory.GameContent.PropickReading");
            oreMapComponentType = AccessTools.TypeByName("Vintagestory.GameContent.OreMapComponent");
            modSystemOreMapType = AccessTools.TypeByName("Vintagestory.GameContent.ModSystemOreMap");

            if (propickType == null || propickReadingType == null || oreMapComponentType == null || modSystemOreMapType == null)
            {
                api.Logger.Warning("[QuickPick] Failed to resolve ore patch types");
                return;
            }

            var modLoaderType = api.ModLoader.GetType();
            getModSystemGenericMethod = modLoaderType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m =>
                    m.Name == "GetModSystem" &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 0);

            genProbeResultsMethod = AccessTools.Method(propickType, "GenProbeResults");
            toHumanReadableMethod = AccessTools.Method(propickReadingType, "ToHumanReadable");
            didProbeMethod = AccessTools.Method(modSystemOreMapType, "DidProbe");

            var printProbeResultsMethod = AccessTools.Method(propickType, "PrintProbeResults");

            oreMapComponentCtor = AccessTools.Constructor(
                oreMapComponentType,
                new[]
                {
                    typeof(int),
                    propickReadingType,
                    AccessTools.TypeByName("Vintagestory.GameContent.OreMapLayer"),
                    typeof(ICoreClientAPI),
                    typeof(string)
                }
            );

            ppwsField = AccessTools.Field(propickType, "ppws");

            var ppwsType = AccessTools.TypeByName("Vintagestory.GameContent.ProPickWorkSpace");
            pageCodesField = ppwsType == null ? null : AccessTools.Field(ppwsType, "pageCodes");

            oreReadingsField = AccessTools.Field(propickReadingType, "OreReadings");
            mentionThresholdField = AccessTools.Field(propickReadingType, "MentionThreshold");
            readingGuidProperty = AccessTools.Property(propickReadingType, "Guid");

            var oreReadingType = AccessTools.TypeByName("Vintagestory.GameContent.OreReading");
            oreReadingTotalFactorField = oreReadingType == null ? null : AccessTools.Field(oreReadingType, "TotalFactor");

            oreMapComponentColorField = AccessTools.Field(oreMapComponentType, "color");

            if (printProbeResultsMethod != null)
            {
                harmony.Patch(
                    printProbeResultsMethod,
                    prefix: new HarmonyMethod(typeof(OreMapLayerPatches), nameof(PrintProbeResultsPrefix))
                );
            }

            if (toHumanReadableMethod != null)
            {
                harmony.Patch(
                    toHumanReadableMethod,
                    prefix: new HarmonyMethod(typeof(OreMapLayerPatches), nameof(ToHumanReadablePrefix))
                );
            }

            if (oreMapComponentCtor != null)
            {
                harmony.Patch(
                    oreMapComponentCtor,
                    postfix: new HarmonyMethod(typeof(OreMapLayerPatches), nameof(OreMapComponentCtorPostfix))
                );
            }

            api.Logger.Notification("[QuickPick] OreMapLayer patches applied");
        }

        public static bool PrintProbeResultsPrefix(
            object __instance,
            IWorldAccessor world,
            IServerPlayer splr,
            ItemSlot itemslot,
            BlockPos pos)
        {
            if (!IsQuickPickMode(__instance, itemslot, splr, pos))
            {
                return true;
            }

            if (genProbeResultsMethod == null)
            {
                api?.Logger.Warning("[QuickPick] GenProbeResults method was null");
                return true;
            }

            object results;
            try
            {
                results = genProbeResultsMethod.Invoke(__instance, new object[] { world, pos });
            }
            catch (Exception ex)
            {
                api?.Logger.Error("[QuickPick] GenProbeResults invoke failed: " + ex);
                return false;
            }

            if (results == null || !IsPropickReading(results))
            {
                splr.SendIngameError("Quickpick failed.");
                return false;
            }

            TagAsQuickPick(results);

            var pageCodes = GetPageCodes(__instance);

            string textResults;
            try
            {
                textResults = (string)toHumanReadableMethod.Invoke(
                    results,
                    new object[] { splr.LanguageCode, pageCodes }
                );
            }
            catch (Exception ex)
            {
                api?.Logger.Error("[QuickPick] ToHumanReadable invoke failed: " + ex);
                splr.SendIngameError("Quickpick failed to format results.");
                return false;
            }

            splr.SendMessage(GlobalConstants.InfoLogChatGroup, textResults, EnumChatType.Notification);

            try
            {
                var oreMapSystem = GetModSystemByType(world.Api, modSystemOreMapType);
                if (oreMapSystem != null && didProbeMethod != null)
                {
                    didProbeMethod.Invoke(oreMapSystem, new object[] { results, splr });
                }
            }
            catch (Exception ex)
            {
                api?.Logger.Error("[QuickPick] DidProbe invoke failed: " + ex);
            }

            return false;
        }

        public static bool ToHumanReadablePrefix(
            object __instance,
            string languageCode,
            Dictionary<string, string> pageCodes,
            ref string __result)
        {
            if (!IsQuickPickReading(__instance))
            {
                return true;
            }

            __result = BuildQuickPickHumanReadable(__instance, languageCode);
            return false;
        }

        public static void OreMapComponentCtorPostfix(
            object __instance,
            int waypointIndex,
            object reading,
            object wpLayer,
            ICoreClientAPI capi,
            string filterByOreCode)
        {
            if (__instance == null) return;
            if (!IsQuickPickReading(reading)) return;
            if (oreMapComponentColorField == null) return;

            oreMapComponentColorField.SetValue(__instance, new Vec4f(0.68f, 0.85f, 1f, 1f));
        }

        private static object GetModSystemByType(ICoreAPI coreApi, Type modSystemType)
        {
            if (coreApi?.ModLoader == null) return null;
            if (modSystemType == null) return null;
            if (getModSystemGenericMethod == null) return null;

            try
            {
                var closedMethod = getModSystemGenericMethod.MakeGenericMethod(modSystemType);
                return closedMethod.Invoke(coreApi.ModLoader, null);
            }
            catch (Exception ex)
            {
                api?.Logger.Warning("[QuickPick] GetModSystemByType failed: " + ex.Message);
                return null;
            }
        }

        private static bool IsQuickPickMode(object instance, ItemSlot slot, IPlayer byPlayer, BlockPos pos)
        {
            if (instance == null) return false;
            if (slot?.Itemstack == null) return false;
            if (quickpickModSystem.PropickType == null) return false;
            if (!quickpickModSystem.PropickType.IsInstanceOfType(instance)) return false;
            if (quickpickModSystem.GetToolModeMethod == null) return false;
            if (quickpickModSystem.ToolModesField == null) return false;

            var blockSel = new BlockSelection { Position = pos };

            int mode;
            try
            {
                mode = (int)quickpickModSystem.GetToolModeMethod.Invoke(
                    instance,
                    new object[] { slot, byPlayer, blockSel }
                );
            }
            catch
            {
                return false;
            }

            var modes = quickpickModSystem.ToolModesField.GetValue(instance) as SkillItem[];
            if (modes == null || mode < 0 || mode >= modes.Length) return false;

            return modes[mode]?.Code?.Path == "quickpick";
        }

        private static Dictionary<string, string> GetPageCodes(object propickInstance)
        {
            var result = new Dictionary<string, string>();

            if (ppwsField == null || pageCodesField == null) return result;

            var ppws = ppwsField.GetValue(propickInstance);
            if (ppws == null) return result;

            return pageCodesField.GetValue(ppws) as Dictionary<string, string> ?? result;
        }

        private static void TagAsQuickPick(object reading)
        {
            var guid = GetReadingGuid(reading);

            if (!string.IsNullOrEmpty(guid) && guid.StartsWith(QuickPickGuidPrefix, StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrEmpty(guid))
            {
                guid = Guid.NewGuid().ToString("N");
            }

            SetReadingGuid(reading, QuickPickGuidPrefix + guid);
        }

        private static string BuildQuickPickHumanReadable(object reading, string languageCode)
        {
            var oreReadingsObj = oreReadingsField?.GetValue(reading);
            if (oreReadingsObj == null)
            {
                return Lang.GetL(languageCode, "propick-noreading");
            }

            double mentionThreshold = 0.002;
            if (mentionThresholdField != null)
            {
                mentionThreshold = (double)mentionThresholdField.GetValue(null);
            }

            var entries = new List<KeyValuePair<double, string>>();

            foreach (var entryObj in (IEnumerable)oreReadingsObj)
            {
                var entryType = entryObj.GetType();
                var keyProp = entryType.GetProperty("Key");
                var valueProp = entryType.GetProperty("Value");
                if (keyProp == null || valueProp == null) continue;

                var oreCode = keyProp.GetValue(entryObj) as string;
                var oreReading = valueProp.GetValue(entryObj);
                if (oreCode == null || oreReading == null) continue;

                double totalFactor = 0;
                if (oreReadingTotalFactorField != null)
                {
                    totalFactor = (double)oreReadingTotalFactorField.GetValue(oreReading);
                }

                if (totalFactor <= mentionThreshold) continue;

                var localizedName = Lang.GetL(languageCode, "ore-" + oreCode);
                entries.Add(new KeyValuePair<double, string>(totalFactor, localizedName));
            }

            if (entries.Count == 0)
            {
                return Lang.GetL(languageCode, "propick-noreading");
            }

            var orderedNames = entries
                .OrderByDescending(e => e.Key)
                .Select(e => e.Value)
                .Distinct()
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine(Lang.GetL(languageCode, "propick-reading-title", orderedNames.Count));
            foreach (var name in orderedNames)
            {
                sb.AppendLine(name);
            }

            return sb.ToString();
        }

        private static bool IsPropickReading(object obj)
        {
            return obj != null && propickReadingType != null && propickReadingType.IsInstanceOfType(obj);
        }

        private static bool IsQuickPickReading(object obj)
        {
            if (!IsPropickReading(obj)) return false;

            var guid = GetReadingGuid(obj);
            return !string.IsNullOrEmpty(guid)
                && guid.StartsWith(QuickPickGuidPrefix, StringComparison.Ordinal);
        }

        private static string GetReadingGuid(object reading)
        {
            return readingGuidProperty?.GetValue(reading) as string;
        }

        private static void SetReadingGuid(object reading, string guid)
        {
            if (readingGuidProperty?.CanWrite == true)
            {
                readingGuidProperty.SetValue(reading, guid);
            }
        }
    }
}