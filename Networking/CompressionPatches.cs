using System;
using System.Reflection;
using HarmonyLib;

namespace SkadiNet
{
    internal static class CompressionDiagnostics
    {
        private const double SummaryIntervalSeconds = 10.0;
        private static int _encoded;
        private static int _decoded;
        private static long _rawEncodeBytes;
        private static long _compressedEncodeBytes;
        private static long _compressedDecodeBytes;
        private static long _rawDecodeBytes;
        private static double _encodeSeconds;
        private static double _decodeSeconds;
        private static double _maxEncodeSeconds;
        private static double _maxDecodeSeconds;
        private static double _nextSummaryTime;

        internal static void RecordEncode(int rawBytes, int compressedBytes, double seconds)
        {
            if (!ModConfig.DebugLogging.Value) return;
            _encoded++;
            _rawEncodeBytes += Math.Max(0, rawBytes);
            _compressedEncodeBytes += Math.Max(0, compressedBytes);
            _encodeSeconds += Math.Max(0, seconds);
            if (seconds > _maxEncodeSeconds) _maxEncodeSeconds = seconds;
            LogIfDue();
        }

        internal static void RecordDecode(int compressedBytes, int rawBytes, double seconds)
        {
            if (!ModConfig.DebugLogging.Value) return;
            _decoded++;
            _compressedDecodeBytes += Math.Max(0, compressedBytes);
            _rawDecodeBytes += Math.Max(0, rawBytes);
            _decodeSeconds += Math.Max(0, seconds);
            if (seconds > _maxDecodeSeconds) _maxDecodeSeconds = seconds;
            LogIfDue();
        }

        private static void LogIfDue()
        {
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            if (now < _nextSummaryTime) return;
            _nextSummaryTime = now + SummaryIntervalSeconds;

            if (_encoded > 0 || _decoded > 0)
            {
                float encodeRatio = _rawEncodeBytes > 0 ? _compressedEncodeBytes / (float)_rawEncodeBytes : 0f;
                Plugin.Log.LogDebug(
                    $"Compression summary {SummaryIntervalSeconds:F0}s: encoded={_encoded} raw={FormatBytes(_rawEncodeBytes)} compressed={FormatBytes(_compressedEncodeBytes)} ratio={encodeRatio:F2} " +
                    $"encodeAvgMs={(_encodeSeconds / Math.Max(1, _encoded)) * 1000.0:F2} encodeMaxMs={_maxEncodeSeconds * 1000.0:F2} " +
                    $"decoded={_decoded} compressedIn={FormatBytes(_compressedDecodeBytes)} rawOut={FormatBytes(_rawDecodeBytes)} " +
                    $"decodeAvgMs={(_decodeSeconds / Math.Max(1, _decoded)) * 1000.0:F2} decodeMaxMs={_maxDecodeSeconds * 1000.0:F2}");
            }

            _encoded = 0;
            _decoded = 0;
            _rawEncodeBytes = 0;
            _compressedEncodeBytes = 0;
            _compressedDecodeBytes = 0;
            _rawDecodeBytes = 0;
            _encodeSeconds = 0;
            _decodeSeconds = 0;
            _maxEncodeSeconds = 0;
            _maxDecodeSeconds = 0;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024 * 1024) return $"{bytes / (1024f * 1024f):F1}MB";
            if (bytes >= 1024) return $"{bytes / 1024f:F1}KB";
            return $"{bytes}B";
        }
    }

    [HarmonyPatch]
    internal static class ZSteamSocketSendCompressionPatch
    {
        private static MethodBase TargetMethod()
        {
            return ReflectionCache.ZSteamSocketSendMethod ?? AccessTools.Method("ZSteamSocket:Send");
        }

        private static void Prefix(object __instance, ref object __0)
        {
            if (!EffectiveConfig.CompressionEnabled)
                return;
            if (__instance == null || __0 == null)
                return;
            if (!FeatureNegotiation.IsCompressionActiveForSocket(__instance))
                return;

            try
            {
                int rawSize = ZPackageTools.Size(__0);
                double started = UnityEngine.Time.realtimeSinceStartupAsDouble;
                if (ZPackageTools.TryBuildCompressedPackage(__0, out object compressed))
                {
                    __0 = compressed;
                    CompressionDiagnostics.RecordEncode(rawSize, ZPackageTools.Size(compressed), UnityEngine.Time.realtimeSinceStartupAsDouble - started);
                }
            }
            catch (Exception ex)
            {
                FeatureNegotiation.RecordCompressionFailure(__instance);
                if (ModConfig.DebugLogging.Value) Plugin.Log.LogWarning($"ZSteamSocket.Send compression failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch]
    internal static class ZSteamSocketRecvCompressionPatch
    {
        private static MethodBase TargetMethod()
        {
            return ReflectionCache.ZSteamSocketRecvMethod ?? AccessTools.Method("ZSteamSocket:Recv");
        }

        private static void Postfix(object __instance, ref object __result)
        {
            if (__result == null)
                return;

            try
            {
                int compressedSize = ZPackageTools.Size(__result);
                double started = UnityEngine.Time.realtimeSinceStartupAsDouble;
                if (ZPackageTools.TryDecompressPackage(__result, out object raw))
                {
                    __result = raw;
                    CompressionDiagnostics.RecordDecode(compressedSize, ZPackageTools.Size(raw), UnityEngine.Time.realtimeSinceStartupAsDouble - started);
                }
            }
            catch (Exception ex)
            {
                FeatureNegotiation.RecordCompressionFailure(__instance);
                if (ModConfig.DebugLogging.Value) Plugin.Log.LogWarning($"ZSteamSocket.Recv decompression failed: {ex.Message}");
            }
        }
    }
}
