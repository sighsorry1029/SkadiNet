using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkadiNet
{
    internal enum ClientCleanupKind
    {
        GarbageCollect
    }

    internal enum ClientCriticalWindow
    {
        InitialSync,
        Teleport,
        FullSnapshotBurst,
        Combat,
        ShipTravel
    }

    internal struct MemoryPressureSnapshot
    {
        public bool Known;
        public ulong TotalMB;
        public ulong AvailableMB;
        public int LoadPercent;

        public override string ToString()
        {
            return Known ? $"load={LoadPercent}% available={AvailableMB}MB total={TotalMB}MB" : "unknown";
        }
    }

    internal static class ClientStutterGuard
    {
        private static readonly Dictionary<ClientCriticalWindow, double> CriticalUntil = new Dictionary<ClientCriticalWindow, double>();
        private static Plugin _plugin;
        private static Coroutine _cleanupCoroutine;
        private static bool _pendingGc;
        private static double _firstPendingSince;
        private static bool _runningCleanup;
        private static bool DelayGcCollect => true;
        private static bool DelayDuringInitialSync => true;
        private static bool DelayDuringTeleport => true;
        private static bool DelayDuringFullSnapshotBurst => true;
        private static bool DelayDuringCombat => true;
        private static bool DelayDuringShipTravel => true;
        private static bool RunCleanupWhenIdle => true;
        private static bool UseMemoryPressureGate => true;

        internal static void Initialize(Plugin plugin)
        {
            _plugin = plugin;
            CriticalUntil.Clear();
            _pendingGc = false;
            _firstPendingSince = 0;
            _runningCleanup = false;

            EnsureCleanupScheduler();
        }

        internal static void Shutdown()
        {
            try
            {
                if (_cleanupCoroutine != null && _plugin != null)
                    _plugin.StopCoroutine(_cleanupCoroutine);
            }
            catch { }
            _cleanupCoroutine = null;
            _plugin = null;
        }

        internal static bool IsActive
        {
            get
            {
                if (!EffectiveConfig.ClientStutterGuardEnabled)
                    return false;
                if (IsDedicatedLike())
                    return false;
                return true;
            }
        }

        private static bool IsDedicatedLike()
        {
            try { if (NetReflection.IsDedicatedServer()) return true; } catch { }
            try
            {
                if (Application.isBatchMode) return true;
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return true;
            }
            catch { }
            return false;
        }

        internal static void MarkCriticalWindow(ClientCriticalWindow window, float seconds)
        {
            if (!IsActive || seconds <= 0f) return;
            if (!IsWindowEnabled(window)) return;

            double until = Time.realtimeSinceStartupAsDouble + seconds;
            if (!CriticalUntil.TryGetValue(window, out double old) || old < until)
                CriticalUntil[window] = until;
        }

        internal static void MarkInitialSyncWindow()
        {
            MarkCriticalWindow(ClientCriticalWindow.InitialSync, Math.Max(0.1f, EffectiveConfig.ClientStutterInitialSyncWindowSeconds));
        }

        internal static void MarkTeleportWindow()
        {
            MarkCriticalWindow(ClientCriticalWindow.Teleport, Math.Max(0.1f, EffectiveConfig.ClientStutterTeleportWindowSeconds));
        }

        internal static void MarkFullSnapshotBurstWindow()
        {
            MarkCriticalWindow(ClientCriticalWindow.FullSnapshotBurst, Math.Max(0.1f, EffectiveConfig.ClientStutterFullSnapshotWindowSeconds));
        }

        internal static void MarkCombatWindow()
        {
            MarkCriticalWindow(ClientCriticalWindow.Combat, Math.Max(0.1f, EffectiveConfig.ClientStutterCombatWindowSeconds));
        }

        internal static void MarkShipTravelWindow()
        {
            MarkCriticalWindow(ClientCriticalWindow.ShipTravel, Math.Max(0.1f, EffectiveConfig.ClientStutterShipWindowSeconds));
        }

        internal static bool TryHandleGcCollect()
        {
            if (!IsActive || !DelayGcCollect || _runningCleanup) return true;
            if (!RunCleanupWhenIdle) return true;

            if (ShouldDelayCleanup(ClientCleanupKind.GarbageCollect, out string reason))
            {
                RequestPending(ClientCleanupKind.GarbageCollect, reason);
                return false;
            }
            return true;
        }

        private static void RequestPending(ClientCleanupKind kind, string reason)
        {
            if (kind == ClientCleanupKind.GarbageCollect) _pendingGc = true;
            if (_firstPendingSince <= 0) _firstPendingSince = Time.realtimeSinceStartupAsDouble;

            EnsureCleanupScheduler();

            if (ModConfig.DebugLogging.Value)
                Plugin.Log.LogDebug($"ClientStutterGuard: deferred {kind} ({reason}).");
        }

        private static void EnsureCleanupScheduler()
        {
            if (_cleanupCoroutine != null) return;
            if (_plugin == null) return;
            if (!IsActive || !RunCleanupWhenIdle) return;
            _cleanupCoroutine = _plugin.StartCoroutine(CleanupScheduler());
        }

        private static IEnumerator CleanupScheduler()
        {
            while (true)
            {
                float wait = Math.Max(0.25f, EffectiveConfig.ClientStutterIdleCleanupPollSeconds);
                yield return new WaitForSeconds(wait);
                if (!IsActive || !RunCleanupWhenIdle) continue;
                TryRunPendingCleanup(false);
            }
        }

        private static bool TryRunPendingCleanup(bool forced)
        {
            if (!_pendingGc) return false;
            if (!IsActive && !forced) return false;
            if (_runningCleanup) return false;

            double now = Time.realtimeSinceStartupAsDouble;
            bool maxDelayExceeded = _firstPendingSince > 0 && now - _firstPendingSince >= Math.Max(1f, EffectiveConfig.ClientStutterMaxDelaySeconds);

            if (!forced && !maxDelayExceeded && TryGetCriticalReason(out string criticalReason))
            {
                if (ModConfig.DebugLogging.Value)
                    Plugin.Log.LogDebug($"ClientStutterGuard: pending cleanup waits for {criticalReason}.");
                return false;
            }

            if (!forced && !maxDelayExceeded && UseMemoryPressureGate && IsMemoryPlentiful(out MemoryPressureSnapshot snapshot))
            {
                if (ModConfig.DebugLogging.Value)
                    Plugin.Log.LogDebug($"ClientStutterGuard: pending cleanup waits; memory plentiful ({snapshot}).");
                return false;
            }

            bool runGc = _pendingGc && DelayGcCollect;
            _pendingGc = false;
            _firstPendingSince = 0;

            _runningCleanup = true;
            try
            {
                if (runGc)
                {
                    if (ModConfig.DebugLogging.Value) Plugin.Log.LogDebug("ClientStutterGuard: running coalesced GC.Collect.");
                    GC.Collect();
                }
            }
            finally
            {
                _runningCleanup = false;
            }
            return runGc;
        }

        private static bool ShouldDelayCleanup(ClientCleanupKind kind, out string reason)
        {
            reason = "safe window";
            if (!IsActive) return false;

            bool maxDelayExceeded = _firstPendingSince > 0 && Time.realtimeSinceStartupAsDouble - _firstPendingSince >= Math.Max(1f, EffectiveConfig.ClientStutterMaxDelaySeconds);
            if (maxDelayExceeded)
            {
                reason = "max delay exceeded";
                return false;
            }

            if (TryGetCriticalReason(out string criticalReason))
            {
                if (UseMemoryPressureGate && IsMemoryPressure(out MemoryPressureSnapshot pressure))
                {
                    reason = $"memory pressure overrides critical window ({pressure})";
                    return false;
                }
                reason = criticalReason;
                return true;
            }

            if (UseMemoryPressureGate && IsMemoryPlentiful(out MemoryPressureSnapshot plentiful))
            {
                reason = $"memory plentiful ({plentiful})";
                return true;
            }

            return false;
        }

        private static bool TryGetCriticalReason(out string reason)
        {
            reason = null;
            double now = Time.realtimeSinceStartupAsDouble;
            double bestUntil = 0;
            ClientCriticalWindow best = ClientCriticalWindow.InitialSync;
            foreach (KeyValuePair<ClientCriticalWindow, double> kv in CriticalUntil)
            {
                if (kv.Value > now && kv.Value > bestUntil && IsWindowEnabled(kv.Key))
                {
                    best = kv.Key;
                    bestUntil = kv.Value;
                }
            }
            if (bestUntil <= now) return false;
            reason = $"{best} for {(bestUntil - now):F1}s";
            return true;
        }

        private static bool IsWindowEnabled(ClientCriticalWindow window)
        {
            switch (window)
            {
                case ClientCriticalWindow.InitialSync: return DelayDuringInitialSync;
                case ClientCriticalWindow.Teleport: return DelayDuringTeleport;
                case ClientCriticalWindow.FullSnapshotBurst: return DelayDuringFullSnapshotBurst;
                case ClientCriticalWindow.Combat: return DelayDuringCombat;
                case ClientCriticalWindow.ShipTravel: return DelayDuringShipTravel;
                default: return true;
            }
        }

        private static bool IsMemoryPlentiful(out MemoryPressureSnapshot snapshot)
        {
            snapshot = GetMemorySnapshot();
            if (!snapshot.Known) return false;
            return snapshot.LoadPercent < Math.Max(1, EffectiveConfig.ClientStutterMemoryPressureThresholdPercent) &&
                   snapshot.AvailableMB >= (ulong)Math.Max(0, EffectiveConfig.ClientStutterMinimumFreeMemoryMB);
        }

        private static bool IsMemoryPressure(out MemoryPressureSnapshot snapshot)
        {
            snapshot = GetMemorySnapshot();
            if (!snapshot.Known) return false;
            return snapshot.LoadPercent >= Math.Max(1, EffectiveConfig.ClientStutterMemoryPressureThresholdPercent) ||
                   snapshot.AvailableMB < (ulong)Math.Max(0, EffectiveConfig.ClientStutterMinimumFreeMemoryMB);
        }

        private static MemoryPressureSnapshot GetMemorySnapshot()
        {
            if (TryGetWindowsMemory(out MemoryPressureSnapshot windows)) return windows;
            if (TryGetProcMemInfo(out MemoryPressureSnapshot linux)) return linux;
            try
            {
                if (SystemInfo.systemMemorySize > 0)
                {
                    ulong total = (ulong)SystemInfo.systemMemorySize;
                    return new MemoryPressureSnapshot { Known = true, TotalMB = total, AvailableMB = total, LoadPercent = 0 };
                }
            }
            catch { }
            return new MemoryPressureSnapshot { Known = false };
        }

        private static bool TryGetWindowsMemory(out MemoryPressureSnapshot snapshot)
        {
            snapshot = new MemoryPressureSnapshot();
            try
            {
                MEMORYSTATUSEX status = new MEMORYSTATUSEX();
                status.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (!GlobalMemoryStatusEx(ref status)) return false;
                snapshot.Known = true;
                snapshot.TotalMB = status.ullTotalPhys / 1024UL / 1024UL;
                snapshot.AvailableMB = status.ullAvailPhys / 1024UL / 1024UL;
                snapshot.LoadPercent = (int)status.dwMemoryLoad;
                return snapshot.TotalMB > 0;
            }
            catch { return false; }
        }

        private static bool TryGetProcMemInfo(out MemoryPressureSnapshot snapshot)
        {
            snapshot = new MemoryPressureSnapshot();
            try
            {
                const string path = "/proc/meminfo";
                if (!File.Exists(path)) return false;
                ulong totalKb = 0;
                ulong availableKb = 0;
                foreach (string line in File.ReadAllLines(path))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal)) totalKb = ParseKb(line);
                    else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal)) availableKb = ParseKb(line);
                }
                if (totalKb == 0 || availableKb == 0) return false;
                snapshot.Known = true;
                snapshot.TotalMB = totalKb / 1024UL;
                snapshot.AvailableMB = availableKb / 1024UL;
                snapshot.LoadPercent = (int)Math.Round(100.0 * (1.0 - (double)availableKb / totalKb));
                return true;
            }
            catch { return false; }
        }

        private static ulong ParseKb(string line)
        {
            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < parts.Length; ++i)
            {
                if (ulong.TryParse(parts[i], out ulong kb)) return kb;
            }
            return 0;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }

    [HarmonyPatch]
    internal static class ClientStutterGuardGcCollectPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo method in typeof(GC).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name == nameof(GC.Collect))
                    yield return method;
            }
        }

        private static bool Prefix()
        {
            return ClientStutterGuard.TryHandleGcCollect();
        }
    }

    [HarmonyPatch]
    internal static class ClientStutterGuardZNetConnectionPatch
    {
        private static MethodBase TargetMethod()
        {
            Type znet = ReflectionCache.ZNetType ?? AccessTools.TypeByName("ZNet");
            Type peer = ReflectionCache.ZNetPeerType ?? AccessTools.TypeByName("ZNetPeer");
            return AccessTools.Method(znet, "OnNewConnection", peer != null ? new[] { peer } : null)
                   ?? AccessTools.Method(znet, "OnNewConnection");
        }

        private static void Postfix()
        {
            ClientStutterGuard.MarkInitialSyncWindow();
        }
    }

    [HarmonyPatch]
    internal static class ClientStutterGuardZdoDataPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type zdoMan = ReflectionCache.ZDOManType ?? AccessTools.TypeByName("ZDOMan");
            if (zdoMan == null) yield break;

            foreach (MethodInfo method in zdoMan.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name == "RPC_ZDOData")
                    yield return method;
            }
        }

        private static void Prefix(object[] __args)
        {
            try
            {
                if (NetReflection.IsServer()) return;
                // Do not extend the window for every tiny incremental ZDOData packet.
                // The guard is aimed at initial/full-sync bursts.
                if (__args == null || ReflectionCache.ZPackageType == null) return;

                foreach (object arg in __args)
                {
                    if (arg == null || !ReflectionCache.ZPackageType.IsInstanceOfType(arg)) continue;
                    if (ZPackageTools.Size(arg) >= 32 * 1024)
                    {
                        ClientStutterGuard.MarkFullSnapshotBurstWindow();
                        return;
                    }
                }
            }
            catch { }
        }
    }

    [HarmonyPatch]
    internal static class ClientStutterGuardLoadingScreenPatch
    {
        private static MethodBase TargetMethod()
        {
            Type scene = AccessTools.TypeByName("ZNetScene");
            return AccessTools.Method(scene, "InLoadingScreen");
        }

        private static void Postfix(bool __result)
        {
            if (__result) ClientStutterGuard.MarkTeleportWindow();
        }
    }

    [HarmonyPatch]
    internal static class ClientStutterGuardShipTravelPatch
    {
        private static FieldInfo _bodyField;

        private static MethodBase TargetMethod()
        {
            Type ship = AccessTools.TypeByName("Ship");
            _bodyField = ReflectionCache.SilentField(ship, "m_body");
            return AccessTools.Method(ship, "CustomFixedUpdate") ?? AccessTools.Method(ship, "FixedUpdate");
        }

        private static void Postfix(object __instance)
        {
            if (__instance == null) return;
            try
            {
                Rigidbody body = _bodyField?.GetValue(__instance) as Rigidbody;
                if (body != null && body.linearVelocity.sqrMagnitude > 4f)
                    ClientStutterGuard.MarkShipTravelWindow();
            }
            catch { }
        }
    }
}
