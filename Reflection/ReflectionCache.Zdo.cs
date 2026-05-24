using HarmonyLib;

namespace SkadiNet
{
    internal static partial class ReflectionCache
    {
        private static void InitializeZdoReflection()
        {
            ZDOManType = AccessTools.TypeByName("ZDOMan");
            ZDOType = AccessTools.TypeByName("ZDO");
            ZDOIDType = AccessTools.TypeByName("ZDOID");
            ZDOExtraDataType = AccessTools.TypeByName("ZDOExtraData");
            ZDOVarsType = AccessTools.TypeByName("ZDOVars");

            ZDOManInstanceField = SilentField(ZDOManType, "instance") ?? SilentField(ZDOManType, "s_instance");
            ZDOManPeersField = SilentField(ZDOManType, "m_peers");
            ZDOManNextSendPeerField = SilentField(ZDOManType, "m_nextSendPeer");
            ZDOManSendTimerField = SilentField(ZDOManType, "m_sendTimer");
            ZDOManSessionIdField = SilentField(ZDOManType, "m_sessionID");
            ZDOObjectsBySectorField = SilentField(ZDOManType, "m_objectsBySector");

            ZDOUidField = SilentField(ZDOType, "m_uid");
            ZDOPrefabField = SilentField(ZDOType, "m_prefab");
            ZDORotationField = SilentField(ZDOType, "m_rotation");
            ZDODataRevisionField = SilentField(ZDOType, "<DataRevision>k__BackingField");
            ZDOOwnerRevisionField = SilentField(ZDOType, "<OwnerRevision>k__BackingField");

            SendZDOsMethod = AccessTools.Method(ZDOManType, "SendZDOs");
            ZDOGetVec3Method = AccessTools.Method(ZDOType, "GetVec3", new[] { typeof(int), typeof(UnityEngine.Vector3) });
            ZDOGetQuaternionMethod = AccessTools.Method(ZDOType, "GetQuaternion", new[] { typeof(int), typeof(UnityEngine.Quaternion) });
            ZDOGetPositionMethod = AccessTools.Method(ZDOType, "GetPosition");
            ZDOGetRotationMethod = AccessTools.Method(ZDOType, "GetRotation");
            ZDOGetOwnerMethod = AccessTools.Method(ZDOType, "GetOwner");
            ZDOSetOwnerMethod = AccessTools.Method(ZDOType, "SetOwner", new[] { typeof(long) });
            ZDOSetOwnerInternalMethod = AccessTools.Method(ZDOType, "SetOwnerInternal", new[] { typeof(long) });
            ZDOGetPrefabMethod = AccessTools.Method(ZDOType, "GetPrefab");
            ZDOPersistentGetter = AccessTools.PropertyGetter(ZDOType, "Persistent") ?? AccessTools.Method(ZDOType, "get_Persistent");
            ZDODistantGetter = AccessTools.PropertyGetter(ZDOType, "Distant") ?? AccessTools.Method(ZDOType, "get_Distant");
            ZDOTypeGetter = AccessTools.PropertyGetter(ZDOType, "Type") ?? AccessTools.Method(ZDOType, "get_Type");
            ZDOIDIsNoneMethod = AccessTools.Method(ZDOIDType, "IsNone");
            ZDOIDUserIDGetter = AccessTools.PropertyGetter(ZDOIDType, "UserID") ?? AccessTools.Method(ZDOIDType, "get_UserID");
            ZDOIDIDGetter = AccessTools.PropertyGetter(ZDOIDType, "ID") ?? AccessTools.Method(ZDOIDType, "get_ID");
            ZDOManForceSendZDOMethod = AccessTools.Method(ZDOManType, "ForceSendZDO", new[] { ZDOIDType }) ?? AccessTools.Method(ZDOManType, "ForceSendZDO");
            ZDOManGetZDOMethod = AccessTools.Method(ZDOManType, "GetZDO", new[] { ZDOIDType }) ?? AccessTools.Method(ZDOManType, "GetZDO");
            ZDOManGetSessionIdMethod = AccessTools.Method(ZDOManType, "GetSessionID");

            InitializeZdoExtraDataReflection();
        }

        private static void InitializeZdoExtraDataReflection()
        {
            if (ZDOExtraDataType == null || ZDOIDType == null) return;

            ZDOExtraDataGetFloatsMethod = AccessTools.Method(ZDOExtraDataType, "GetFloats", new[] { ZDOIDType });
            ZDOExtraDataGetVec3sMethod = AccessTools.Method(ZDOExtraDataType, "GetVec3s", new[] { ZDOIDType });
            ZDOExtraDataGetQuaternionsMethod = AccessTools.Method(ZDOExtraDataType, "GetQuaternions", new[] { ZDOIDType });
            ZDOExtraDataGetIntsMethod = AccessTools.Method(ZDOExtraDataType, "GetInts", new[] { ZDOIDType });
            ZDOExtraDataGetLongsMethod = AccessTools.Method(ZDOExtraDataType, "GetLongs", new[] { ZDOIDType });
            ZDOExtraDataGetStringsMethod = AccessTools.Method(ZDOExtraDataType, "GetStrings", new[] { ZDOIDType });
            ZDOExtraDataGetByteArraysMethod = AccessTools.Method(ZDOExtraDataType, "GetByteArrays", new[] { ZDOIDType });
            ZDOExtraDataGetConnectionMethod = AccessTools.Method(ZDOExtraDataType, "GetConnection", new[] { ZDOIDType });
        }
    }
}
