using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using ServerSync;

namespace SkadiNet
{
    internal enum ConfigSyncScope
    {
        ServerSynced,
        ClientLocal,
        MigrationOnly
    }

    internal static class ConfigSyncManager
    {
        private static MethodInfo _addConfigEntryMethod;

        internal static ConfigSync Sync { get; private set; }
        internal static ConfigEntry<bool> LockServerConfig { get; private set; }

        internal static void Initialize(ConfigFile config)
        {
            Sync = new ConfigSync(Plugin.PluginGuid)
            {
                DisplayName = Plugin.PluginName,
                CurrentVersion = Plugin.PluginVersion,
                MinimumRequiredVersion = Plugin.PluginVersion,
                ModRequired = true
            };

            LockServerConfig = config.Bind(
                "General",
                "LockServerConfig",
                true,
                new ConfigDescription("Lock server-synced config for non-admin clients.", null, new ConfigurationManagerAttributes { Order = 90 })
            );

            SyncedConfigEntry<bool> lockingEntry = Sync.AddLockingConfigEntry(LockServerConfig);
            lockingEntry.SynchronizedConfig = true;
        }

        internal static ConfigEntry<T> Bind<T>(ConfigFile config, string group, string name, T value, string description, ConfigSyncScope scope)
        {
            return Bind(config, group, name, value, new ConfigDescription(description), scope);
        }

        internal static ConfigEntry<T> Bind<T>(ConfigFile config, string group, string name, T value, ConfigDescription description, ConfigSyncScope scope)
        {
            string syncSuffix = SuffixFor(scope);
            var extendedDescription = new ConfigDescription(
                description.Description + syncSuffix,
                description.AcceptableValues,
                description.Tags
            );

            ConfigEntry<T> entry = config.Bind(group, name, value, extendedDescription);
            if (scope != ConfigSyncScope.MigrationOnly)
                Register(entry, scope == ConfigSyncScope.ServerSynced);
            return entry;
        }

        private static string SuffixFor(ConfigSyncScope scope)
        {
            switch (scope)
            {
                case ConfigSyncScope.ServerSynced:
                    return " [Synced with Server]";
                case ConfigSyncScope.ClientLocal:
                    return " [Client Local; Not Synced with Server]";
                case ConfigSyncScope.MigrationOnly:
                    return " [Migration Only; Not Synced with Server]";
                default:
                    throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
            }
        }

        private static void Register(ConfigEntryBase entry, bool synchronized)
        {
            if (Sync == null || entry == null) return;
            if (LockServerConfig != null && Equals(entry.Definition, LockServerConfig.Definition)) return;

            MethodInfo addMethod = GetAddConfigEntryMethod().MakeGenericMethod(entry.SettingType);
            object synced = addMethod.Invoke(Sync, new object[] { entry });
            if (synced is OwnConfigEntryBase ownEntry)
                ownEntry.SynchronizedConfig = synchronized;
        }

        private static MethodInfo GetAddConfigEntryMethod()
        {
            if (_addConfigEntryMethod != null) return _addConfigEntryMethod;

            _addConfigEntryMethod = typeof(ConfigSync)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(method => method.Name == nameof(ConfigSync.AddConfigEntry) && method.IsGenericMethodDefinition);

            return _addConfigEntryMethod;
        }
    }
}
