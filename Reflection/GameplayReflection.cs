using System;

namespace SkadiNet
{
    internal static class GameplayReflection
    {
        internal static void Initialize()
        {
        }

        internal static object GetZdoFromNView(object nview)
        {
            if (nview == null) return null;
            try { return ReflectionCache.ZNetViewGetZDOMethod?.Invoke(nview, null) ?? ReflectionCache.ZNetViewZdoField?.GetValue(nview); }
            catch { return null; }
        }

        internal static object GetNViewFromCharacterLike(object instance)
        {
            if (instance == null) return null;
            try { return ReflectionCache.CharacterNViewField?.GetValue(instance); } catch { }
            try { return ReflectionCache.MonsterAINViewField?.GetValue(instance); } catch { }
            try { return ReflectionCache.CachedField(instance.GetType(), "m_nview")?.GetValue(instance); } catch { }
            return null;
        }

        internal static object GetZdoFromCharacterLike(object instance)
        {
            return GetZdoFromNView(GetNViewFromCharacterLike(instance));
        }

        internal static bool TryGetPlayerId(object player, out long id)
        {
            id = 0;
            if (player == null || ReflectionCache.PlayerGetPlayerIDMethod == null) return false;
            try
            {
                object value = ReflectionCache.PlayerGetPlayerIDMethod.Invoke(player, null);
                return ReflectionCache.TryConvertToLong(value, out id);
            }
            catch { }
            return false;
        }

        internal static bool LooksLikePlayer(object obj)
        {
            if (obj == null) return false;
            Type t = obj.GetType();
            return t.Name == "Player" || (ReflectionCache.PlayerType != null && ReflectionCache.PlayerType.IsAssignableFrom(t));
        }
    }
}
