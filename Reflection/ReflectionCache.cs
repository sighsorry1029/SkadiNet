using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SkadiNet
{
    internal static partial class ReflectionCache
    {
        internal static Type ZNetType;
        internal static Type ZNetPeerType;
        internal static Type ZDOManType;
        internal static Type ZDOType;
        internal static Type ZDOIDType;
        internal static Type ZDOExtraDataType;
        internal static Type ZRpcType;
        internal static Type ZPackageType;
        internal static Type ZSteamSocketType;
        internal static Type ZNetViewType;
        internal static Type MonsterAIType;
        internal static Type PlayerType;
        internal static Type ZDOVarsType;
        internal static Type ZRoutedRpcType;
        internal static Type RoutedRPCDataType;

        internal static FieldInfo ZDOManPeersField;
        internal static FieldInfo ZDOManNextSendPeerField;
        internal static FieldInfo ZDOManSendTimerField;
        internal static FieldInfo ZDOManSessionIdField;
        internal static FieldInfo ZNetInstanceField;
        internal static FieldInfo ZNetPeersField;
        internal static FieldInfo ZDOManInstanceField;
        internal static FieldInfo PeerRpcField;
        internal static FieldInfo PeerUidField;
        internal static FieldInfo PeerRefPosField;
        internal static FieldInfo ZNetPeerRpcField;
        internal static FieldInfo ZNetPeerUidField;
        internal static FieldInfo ZNetPeerRefPosField;
        internal static FieldInfo ZNetViewZdoField;
        internal static FieldInfo CharacterNViewField;
        internal static FieldInfo MonsterAINViewField;
        internal static FieldInfo ZDOUidField;
        internal static FieldInfo ZDOPrefabField;
        internal static FieldInfo ZDORotationField;
        internal static FieldInfo ZDODataRevisionField;
        internal static FieldInfo ZDOOwnerRevisionField;
        internal static FieldInfo ZDOObjectsBySectorField;
        internal static FieldInfo ZSteamSocketSendQueueField;
        internal static FieldInfo RoutedRpcDataSenderPeerIdField;
        internal static FieldInfo RoutedRpcDataTargetPeerIdField;
        internal static FieldInfo RoutedRpcDataTargetZdoField;
        internal static FieldInfo RoutedRpcDataMethodHashField;
        internal static FieldInfo RoutedRpcDataParametersField;

        internal static MethodInfo ZNetIsServerMethod;
        internal static MethodInfo ZNetIsDedicatedMethod;
        internal static MethodInfo ZNetGetPeersMethod;
        internal static MethodInfo ZNetGetReferencePositionMethod;
        internal static MethodInfo SendZDOsMethod;
        internal static MethodInfo ZDOGetVec3Method;
        internal static MethodInfo ZDOGetQuaternionMethod;
        internal static MethodInfo ZDOGetPositionMethod;
        internal static MethodInfo ZDOGetRotationMethod;
        internal static MethodInfo ZDOGetOwnerMethod;
        internal static MethodInfo ZDOSetOwnerMethod;
        internal static MethodInfo ZDOSetOwnerInternalMethod;
        internal static MethodInfo ZDOGetPrefabMethod;
        internal static MethodInfo ZDOPersistentGetter;
        internal static MethodInfo ZDODistantGetter;
        internal static MethodInfo ZDOTypeGetter;
        internal static MethodInfo ZDOIDIsNoneMethod;
        internal static MethodInfo ZDOIDUserIDGetter;
        internal static MethodInfo ZDOIDIDGetter;
        internal static MethodInfo PlayerGetPlayerIDMethod;
        internal static MethodInfo ZNetViewGetZDOMethod;
        internal static MethodInfo ZNetViewClaimOwnershipMethod;
        internal static MethodInfo ZDOManForceSendZDOMethod;
        internal static MethodInfo ZDOManGetZDOMethod;
        internal static MethodInfo ZDOManGetSessionIdMethod;
        internal static MethodInfo ZRpcGetSocketMethod;
        internal static MethodInfo ZRpcInvokeMethod;
        internal static MethodInfo ZRpcRegisterGenericPackageMethod;
        internal static MethodInfo ZRpcUnregisterMethod;
        internal static MethodInfo ZSteamSocketSendMethod;
        internal static MethodInfo ZSteamSocketRecvMethod;
        internal static MethodInfo ZSteamSocketGetSendQueueSizeMethod;
        internal static MethodInfo RoutedRpcDataSerializeMethod;
        internal static MethodInfo RoutedRpcDataDeserializeMethod;
        internal static MethodInfo ZRoutedRpcRouteRPCMethod;

        internal static MethodInfo ZDOExtraDataGetFloatsMethod;
        internal static MethodInfo ZDOExtraDataGetVec3sMethod;
        internal static MethodInfo ZDOExtraDataGetQuaternionsMethod;
        internal static MethodInfo ZDOExtraDataGetIntsMethod;
        internal static MethodInfo ZDOExtraDataGetLongsMethod;
        internal static MethodInfo ZDOExtraDataGetStringsMethod;
        internal static MethodInfo ZDOExtraDataGetByteArraysMethod;
        internal static MethodInfo ZDOExtraDataGetConnectionMethod;

        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> FieldCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();
        private static readonly Dictionary<Type, KeyValueEntryAccessors> KeyValueAccessorsByType = new Dictionary<Type, KeyValueEntryAccessors>();

        internal static void Initialize()
        {
            FieldCache.Clear();
            KeyValueAccessorsByType.Clear();

            InitializeNetReflection();
            InitializeZdoReflection();
            InitializeRpcReflection();
            InitializeGameplayReflection();
            NetReflection.Initialize();
            ZdoReflection.Initialize();
            RpcReflection.Initialize();
            GameplayReflection.Initialize();
            ZPackageTools.Initialize();
            LogCapabilitySummary();
        }

        private static FieldInfo FieldByQualifiedName(string typeAndField)
        {
            if (string.IsNullOrEmpty(typeAndField)) return null;
            int split = typeAndField.LastIndexOf(':');
            if (split <= 0 || split >= typeAndField.Length - 1) return null;

            Type type = AccessTools.TypeByName(typeAndField.Substring(0, split));
            return SilentField(type, typeAndField.Substring(split + 1));
        }

        internal static FieldInfo SilentField(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, flags);
                if (field != null) return field;
            }

            return null;
        }

    }
}
