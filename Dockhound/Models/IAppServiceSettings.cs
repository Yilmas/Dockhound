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
        RestrictedAccessSettings GetRestrictedAccess();
        EnvironmentState GetCurrentEnvironment();
        Task UpdateRestrictedAccessAsync(ulong? channelId, ulong? messageId, CancellationToken ct = default);
    }
}
