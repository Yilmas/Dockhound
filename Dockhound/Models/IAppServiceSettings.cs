using Dockhound.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Models
{
    public interface IAppSettingsService
    {
        [Obsolete("Warning GUILD SPECIFIC", true)]
        RestrictedAccessSettings GetRestrictedAccess();
        EnvironmentState GetCurrentEnvironment();
        [Obsolete("Warning GUILD SPECIFIC", true)]
        Task UpdateRestrictedAccessAsync(ulong? channelId, ulong? messageId, CancellationToken ct = default);
    }
}
