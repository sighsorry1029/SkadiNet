using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SkadiNet
{
    internal static class ZPackageTools
    {
        internal const int CompressionMagic = 0x434E4B53; // "SKNC" little-endian
        internal const int CompressionProtocol = 1;
        internal const int CompressionAlgoDeflate = 1;

        private static ConstructorInfo _ctorEmpty;
        private static ConstructorInfo _ctorBytes;
        private static MethodInfo _writeInt;
        private static MethodInfo _writeUInt16;
        private static MethodInfo _writeByte;
        private static MethodInfo _writeFloat;
        private static MethodInfo _writeLong;
        private static MethodInfo _writeString;
        private static MethodInfo _writeByteArray;
        private static MethodInfo _writeVector3;
        private static MethodInfo _writeZdoId;
        private static MethodInfo _readInt;
        private static MethodInfo _readByte;
        private static MethodInfo _readByteArray;
        private static MethodInfo _readString;
        private static MethodInfo _getArray;
        private static MethodInfo _size;
        private static MethodInfo _setPos;
        private static MethodInfo _getPos;
        private static MethodInfo _loadBytes;

        internal static void Initialize()
        {
            Type t = ReflectionCache.ZPackageType;
            if (t == null) return;

            _ctorEmpty = AccessTools.Constructor(t, Type.EmptyTypes);
            _ctorBytes = AccessTools.Constructor(t, new[] { typeof(byte[]) });

            _writeInt = AccessTools.Method(t, "Write", new[] { typeof(int) });
            _writeUInt16 = AccessTools.Method(t, "Write", new[] { typeof(ushort) });
            _writeByte = AccessTools.Method(t, "Write", new[] { typeof(byte) });
            _writeFloat = AccessTools.Method(t, "Write", new[] { typeof(float) });
            _writeLong = AccessTools.Method(t, "Write", new[] { typeof(long) });
            _writeString = AccessTools.Method(t, "Write", new[] { typeof(string) });
            _writeByteArray = AccessTools.Method(t, "Write", new[] { typeof(byte[]) });
            _writeVector3 = AccessTools.Method(t, "Write", new[] { typeof(Vector3) });
            _writeZdoId = ReflectionCache.ZDOIDType != null ? AccessTools.Method(t, "Write", new[] { ReflectionCache.ZDOIDType }) : null;

            _readInt = AccessTools.Method(t, "ReadInt");
            _readByte = AccessTools.Method(t, "ReadByte");
            _readByteArray = AccessTools.Method(t, "ReadByteArray", Type.EmptyTypes);
            _readString = AccessTools.Method(t, "ReadString");
            _getArray = AccessTools.Method(t, "GetArray");
            _size = AccessTools.Method(t, "Size");
            _setPos = AccessTools.Method(t, "SetPos", new[] { typeof(int) });
            _getPos = AccessTools.Method(t, "GetPos");
            _loadBytes = AccessTools.Method(t, "Load", new[] { typeof(byte[]) });
        }

        internal static object NewPackage()
        {
            return _ctorEmpty?.Invoke(null) ?? Activator.CreateInstance(ReflectionCache.ZPackageType);
        }

        internal static object NewPackage(byte[] bytes)
        {
            if (_ctorBytes != null) return _ctorBytes.Invoke(new object[] { bytes });
            object pkg = NewPackage();
            _loadBytes?.Invoke(pkg, new object[] { bytes });
            return pkg;
        }

        internal static bool TryAppendPackagePayload(object target, object source)
        {
            if (target == null || source == null || _writeByte == null || _loadBytes == null || _getArray == null)
                return false;

            byte[] before = GetArray(target);
            int oldPos = GetPos(target);
            byte[] payload = GetArray(source);
            if (payload == null || payload.Length == 0)
                return false;

            try
            {
                for (int i = 0; i < payload.Length; i++)
                    WriteByte(target, payload[i]);
                return true;
            }
            catch
            {
                try
                {
                    _loadBytes.Invoke(target, new object[] { before ?? Array.Empty<byte>() });
                    SetPos(target, oldPos);
                }
                catch { }
                return false;
            }
        }

        internal static int Size(object pkg)
        {
            try { return _size != null && pkg != null ? (int)_size.Invoke(pkg, null) : 0; }
            catch { return 0; }
        }

        internal static byte[] GetArray(object pkg)
        {
            try { return _getArray?.Invoke(pkg, null) as byte[] ?? Array.Empty<byte>(); }
            catch { return Array.Empty<byte>(); }
        }

        internal static int GetPos(object pkg)
        {
            try { return _getPos != null ? (int)_getPos.Invoke(pkg, null) : 0; }
            catch { return 0; }
        }

        internal static void SetPos(object pkg, int pos)
        {
            try { _setPos?.Invoke(pkg, new object[] { pos }); }
            catch { }
        }

        internal static void WriteInt(object pkg, int v) => _writeInt.Invoke(pkg, new object[] { v });
        internal static void WriteUShort(object pkg, ushort v) => _writeUInt16.Invoke(pkg, new object[] { v });
        internal static void WriteByte(object pkg, byte v) => _writeByte.Invoke(pkg, new object[] { v });
        internal static void WriteFloat(object pkg, float v) => _writeFloat.Invoke(pkg, new object[] { v });
        internal static void WriteLong(object pkg, long v) => _writeLong.Invoke(pkg, new object[] { v });
        internal static void WriteString(object pkg, string v) => _writeString.Invoke(pkg, new object[] { v ?? string.Empty });
        internal static void WriteByteArray(object pkg, byte[] v) => _writeByteArray.Invoke(pkg, new object[] { v ?? Array.Empty<byte>() });
        internal static void WriteVector3(object pkg, Vector3 v) => _writeVector3.Invoke(pkg, new object[] { v });
        internal static void WriteZdoId(object pkg, object zdoId) => _writeZdoId.Invoke(pkg, new[] { zdoId });

        internal static int ReadInt(object pkg) => (int)_readInt.Invoke(pkg, null);
        internal static byte ReadByte(object pkg) => (byte)_readByte.Invoke(pkg, null);
        internal static string ReadString(object pkg) => (string)_readString.Invoke(pkg, null);
        internal static byte[] ReadByteArray(object pkg) => _readByteArray.Invoke(pkg, null) as byte[] ?? Array.Empty<byte>();

        internal static bool TryBuildCompressedPackage(object original, out object compressedPackage)
        {
            compressedPackage = null;
            try
            {
                byte[] raw = GetArray(original);
                if (raw == null || raw.Length < Math.Max(64, EffectiveConfig.CompressionThresholdBytes))
                    return false;
                if (LooksCompressed(original))
                    return false;

                byte[] compressed = Deflate(raw);
                if (compressed == null || compressed.Length <= 0)
                    return false;

                float ratio = compressed.Length / (float)Math.Max(1, raw.Length);
                if (ratio >= EffectiveConfig.CompressionMinUsefulRatio)
                    return false;

                object wrapper = NewPackage();
                WriteInt(wrapper, CompressionMagic);
                WriteInt(wrapper, CompressionProtocol);
                WriteInt(wrapper, CompressionAlgoDeflate);
                WriteInt(wrapper, raw.Length);
                WriteByteArray(wrapper, compressed);
                compressedPackage = wrapper;
                return true;
            }
            catch (Exception ex)
            {
                if (ModConfig.DebugLogging.Value) Plugin.Log.LogWarning($"Compression encode failed: {ex.Message}");
                return false;
            }
        }

        internal static bool TryDecompressPackage(object maybeCompressed, out object rawPackage)
        {
            rawPackage = maybeCompressed;
            if (maybeCompressed == null) return false;

            int oldPos = GetPos(maybeCompressed);
            try
            {
                SetPos(maybeCompressed, 0);
                if (ReadInt(maybeCompressed) != CompressionMagic)
                {
                    SetPos(maybeCompressed, oldPos);
                    return false;
                }

                int protocol = ReadInt(maybeCompressed);
                int algo = ReadInt(maybeCompressed);
                int originalSize = ReadInt(maybeCompressed);
                byte[] compressed = ReadByteArray(maybeCompressed);

                if (protocol != CompressionProtocol || algo != CompressionAlgoDeflate || originalSize < 0)
                    throw new InvalidDataException($"Unsupported SkadiNet compression header protocol={protocol}, algo={algo}, size={originalSize}");

                byte[] raw = Inflate(compressed, originalSize);
                rawPackage = NewPackage(raw);
                return true;
            }
            catch (Exception ex)
            {
                SetPos(maybeCompressed, oldPos);
                if (ModConfig.DebugLogging.Value) Plugin.Log.LogWarning($"Compression decode failed: {ex.Message}");
                return false;
            }
        }

        internal static bool LooksCompressed(object pkg)
        {
            if (pkg == null) return false;
            int oldPos = GetPos(pkg);
            try
            {
                if (Size(pkg) < 16) return false;
                SetPos(pkg, 0);
                return ReadInt(pkg) == CompressionMagic;
            }
            catch { return false; }
            finally { SetPos(pkg, oldPos); }
        }

        private static byte[] Deflate(byte[] raw)
        {
            using (var output = new MemoryStream())
            {
                using (var ds = new DeflateStream(output, System.IO.Compression.CompressionLevel.Fastest, true))
                    ds.Write(raw, 0, raw.Length);
                return output.ToArray();
            }
        }

        private static byte[] Inflate(byte[] compressed, int expectedSize)
        {
            using (var input = new MemoryStream(compressed))
            using (var ds = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream(expectedSize > 0 ? expectedSize : 0))
            {
                ds.CopyTo(output);
                byte[] raw = output.ToArray();
                if (expectedSize > 0 && raw.Length != expectedSize)
                    throw new InvalidDataException($"Inflated size mismatch expected={expectedSize}, actual={raw.Length}");
                return raw;
            }
        }
    }
}
