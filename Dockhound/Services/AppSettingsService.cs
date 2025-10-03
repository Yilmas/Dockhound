using Dockhound.Enums;
using Dockhound.Models;
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

namespace Dockhound.Services
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

        public EnvironmentState GetCurrentEnvironment()
        => _options.CurrentValue.Configuration.Environment;

        public void Save(AppSettings appSettings)
        {
            var json = JsonConvert.SerializeObject(appSettings, Formatting.Indented);
            File.WriteAllText(_appSettingsPath, json);
        }
    }
}
