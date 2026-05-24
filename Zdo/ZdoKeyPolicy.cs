using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SkadiNet
{
    internal static class ZdoKeyPolicy
    {
        private static readonly HashSet<int> CriticalHashes = new HashSet<int>();
        private static readonly HashSet<int> VelocityLikeHashes = new HashSet<int>();
        private static readonly HashSet<int> PlayerLikeHashes = new HashSet<int>();
        private static readonly HashSet<int> ShipLikeHashes = new HashSet<int>();

        internal static void Initialize()
        {
            CriticalHashes.Clear();
            VelocityLikeHashes.Clear();
            PlayerLikeHashes.Clear();
            ShipLikeHashes.Clear();

            Type vars = ZdoReflection.ZDOVarsType;
            if (vars == null) return;

            foreach (FieldInfo field in vars.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (field.FieldType != typeof(int)) continue;
                int hash;
                try { hash = (int)field.GetValue(null); }
                catch { continue; }

                string n = field.Name.ToLowerInvariant();

                bool critical =
                    n.Contains("owner") || n.Contains("target") || n.Contains("health") || n.Contains("level") ||
                    n.Contains("spawn") || n.Contains("dead") || n.Contains("attack") || n.Contains("tame") ||
                    n.Contains("rider") || n.Contains("rudder") || n.Contains("bodyvel") || n.Contains("velocity") || n.Contains("velhash");

                bool velocityLike = n.Contains("vel") || n.Contains("velocity") || n.Contains("body") || n.Contains("initvel");

                // Used only by Profile A ownership scanner. False positives are safer than stealing player ZDOs.
                bool playerLike =
                    n.Contains("player") || n.Contains("username") || n.Contains("playername") ||
                    n.Contains("emote") || n.Contains("hair") || n.Contains("beard") ||
                    n.Contains("skin") || n.Contains("modelindex") || n.Contains("gender");

                bool shipLike =
                    n.Contains("ship") || n.Contains("rudder") || n.Contains("sail") ||
                    n.Contains("anchor") || n.Contains("waterlevel") || n.Contains("inwater") ||
                    n.Contains("forward") || n.Contains("ashlandsailer");

                if (critical) CriticalHashes.Add(hash);
                if (velocityLike) VelocityLikeHashes.Add(hash);
                if (playerLike) PlayerLikeHashes.Add(hash);
                if (shipLike) ShipLikeHashes.Add(hash);
            }

            if (ModConfig.DebugLogging.Value)
                Plugin.Log.LogInfo($"ZDO key policy initialized: critical={CriticalHashes.Count}, velocityLike={VelocityLikeHashes.Count}, playerLike={PlayerLikeHashes.Count}, shipLike={ShipLikeHashes.Count}");
        }

        internal static bool ShouldNeverCull(int hash)
        {
            if (CriticalHashes.Contains(hash)) return true;
            if (VelocityLikeHashes.Contains(hash)) return true;
            return false;
        }

        internal static bool LooksPlayerLike(object zdo)
        {
            return HasAnyHash(zdo, PlayerLikeHashes);
        }

        internal static bool LooksShipLike(object zdo)
        {
            return HasAnyHash(zdo, ShipLikeHashes);
        }

        private static bool HasAnyHash(object zdo, HashSet<int> hashes)
        {
            if (zdo == null || hashes == null || hashes.Count == 0) return false;
            object id = ZdoReflection.GetIdObject(zdo);
            if (id == null) return false;

            return HasAnyHashInGroup(ReflectionCache.ZDOExtraDataGetFloatsMethod, id, hashes) ||
                   HasAnyHashInGroup(ReflectionCache.ZDOExtraDataGetVec3sMethod, id, hashes) ||
                   HasAnyHashInGroup(ReflectionCache.ZDOExtraDataGetQuaternionsMethod, id, hashes) ||
                   HasAnyHashInGroup(ReflectionCache.ZDOExtraDataGetIntsMethod, id, hashes) ||
                   HasAnyHashInGroup(ReflectionCache.ZDOExtraDataGetLongsMethod, id, hashes) ||
                   HasAnyHashInGroup(ReflectionCache.ZDOExtraDataGetStringsMethod, id, hashes) ||
                   HasAnyHashInGroup(ReflectionCache.ZDOExtraDataGetByteArraysMethod, id, hashes);
        }

        private static bool HasAnyHashInGroup(MethodInfo method, object id, HashSet<int> hashes)
        {
            if (method == null || id == null) return false;
            try
            {
                object list = method.Invoke(null, new[] { id });
                if (!(list is IEnumerable enumerable)) return false;
                foreach (object entry in enumerable)
                {
                    if (entry == null) continue;
                    if (!ReflectionCache.TryReadKeyValueKey(entry, out int key)) continue;
                    if (hashes.Contains(key)) return true;
                }
            }
            catch { }
            return false;
        }
    }
}
