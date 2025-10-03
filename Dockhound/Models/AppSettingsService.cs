using Dockhound.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Dockhound.Models
{
    public class AppSettingsService : IAppSettingsService
    {
        private readonly IOptionsMonitor<AppSettings> _options;
        private readonly IConfigurationRoot _configRoot;
        private readonly string _appSettingsPath;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public AppSettingsService(IOptionsMonitor<AppSettings> options, IConfiguration config)
        {
            _options = options;
            _configRoot = (IConfigurationRoot)config;
            _appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        }

        [Obsolete("Warning GUILD SPECIFIC", true)]
        public RestrictedAccessSettings GetRestrictedAccess()
        => _options.CurrentValue.Verify.RestrictedAccess ?? new RestrictedAccessSettings();

        public EnvironmentState GetCurrentEnvironment()
        => _options.CurrentValue.Configuration.Environment;

        public void Save(AppSettings appSettings)
        {
            var json = JsonConvert.SerializeObject(appSettings, Formatting.Indented);
            File.WriteAllText(_appSettingsPath, json);
        }

        /// <summary>
        /// Update Verify:RestrictedAccess ChannelId/MessageId in appsettings.json and reload config.
        /// </summary>
        [Obsolete("Warning GUILD SPECIFIC", true)]
        public async Task UpdateRestrictedAccessAsync(ulong? channelId, ulong? messageId, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var json = await File.ReadAllTextAsync(_appSettingsPath, ct);
                var root = JsonNode.Parse(json) as JsonObject ?? new JsonObject();

                // Ensure Verify object exists
                if (root["Verify"] is not JsonObject verify)
                {
                    verify = new JsonObject();
                    root["Verify"] = verify;
                }

                // Ensure RestrictedAccess object exists
                if (verify["RestrictedAccess"] is not JsonObject ra)
                {
                    ra = new JsonObject();
                    verify["RestrictedAccess"] = ra;
                }

                // Set values (null -> JSON null)
                ra["ChannelId"] = channelId is null ? null : JsonValue.Create(channelId.Value);
                ra["MessageId"] = messageId is null ? null : JsonValue.Create(messageId.Value);

                var updated = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_appSettingsPath, updated, ct);

                // Hot-reload bound options
                _configRoot.Reload();
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
