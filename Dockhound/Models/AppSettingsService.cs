using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dockhound.Enums;

namespace Dockhound.Models
{
    public static class AppSettingsService
    {
        private const string ConfigPath = "appsettings.json";

        private static AppSettings Load()
        {
            if (!File.Exists(ConfigPath))
                throw new FileNotFoundException("Configuration file not found.", ConfigPath);

            var json = File.ReadAllText(ConfigPath);
            var settings = JsonConvert.DeserializeObject<AppSettings>(json);

            if (settings == null)
                throw new Exception("Failed to deserialize appsettings.json");

            return settings;
        }

        private static void Save(AppSettings appSettings)
        {
            var json = JsonConvert.SerializeObject(appSettings, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }

        public static RestrictedAccessSettings GetRestrictedAccess()
        {
            var config = Load();
            return config.Verify.RestrictedAccess;
        }

        public static void UpdateRestrictedAccess()
        {
            UpdateRestrictedAccess(null, null);
        }

        public static void UpdateRestrictedAccess(ulong? channelId, ulong? messageId)
        {
            var config = Load();
            var ra = config.Verify.RestrictedAccess;
            ra.ChannelId = channelId;
            ra.MessageId = messageId;
            Save(config);
        }

        public static EnvironmentState GetCurrentEnvironment()
        {
            string? env = Environment.GetEnvironmentVariable("WLL_ENVIRONMENT");

            if (string.IsNullOrEmpty(env))
            {
                return EnvironmentState.Development; // Default to Development if not set
            }

            return Enum.TryParse(env, out EnvironmentState result) ? result : EnvironmentState.Development;
        }
    }
}
