using Dockhound.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Models
{
    public class AppSettings
    {
        public BotConfiguration Configuration { get; set; }
        public VerifySettings Verify { get; set; }
        public ApplicantSettings Applicant { get; set; }
    }

    public class BotConfiguration
    {
        public string DiscordToken { get; set; }
        public string DatabaseConnectionString { get; set; }
        public string AppInsightsConnectionString { get; set; }
        public EnvironmentState Environment { get; set; } = EnvironmentState.Development;
    }

    public class VerifySettings
    {
        public string ImageUrl { get; set; }
        public ulong ReviewChannelId { get; set; }
        public ulong NotificationChannelId { get; set; }
        public ulong ColonialSecureChannelId { get; set; }
        public ulong WardenSecureChannelId { get; set; }
        public List<ulong>? RecruitAssignerRoles { get; set; }
        public List<ulong>? AllyAssignerRoles { get; set; }
        public RestrictedAccessSettings RestrictedAccess { get; set; }
    }

    public class RestrictedAccessSettings
    {
        public List<ulong>? AlwaysRestrictRoles { get; set; }
        public List<ulong>? MemberOnlyRoles { get; set; }
        public ulong? ChannelId { get; set; }
        public ulong? MessageId { get; set; }
    }

    public class ApplicantSettings
    {
        public ulong ForumChannelId { get; set; }
        public ulong PendingTagChannelId { get; set; }
        public List<ulong>? AllowedAssignerRoleIds { get; set; }
    }

}
