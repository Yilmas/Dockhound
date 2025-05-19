using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WLL_Tracker.Models
{
    public class RootConfig
    {
        public AppSettings AppSettings { get; set; }
    }

    public class AppSettings
    {
        public VerifySettings Verify { get; set; }
        public ApplicantSettings Applicant { get; set; }
    }

    public class VerifySettings
    {
        public string ImageUrl { get; set; }
        public ulong ReviewChannelId { get; set; }
        public ulong NotificationChannelId { get; set; }
        public ulong ColonialSecureChannelId { get; set; }
        public ulong WardenSecureChannelId { get; set; }
        public RestrictedAccessSettings RestrictedAccess { get; set; }
    }

    public class RestrictedAccessSettings
    {
        public ulong? ChannelId { get; set; }
        public ulong? MessageId { get; set; }
        public List<ulong>? Whitelist { get; set; }
    }

    public class ApplicantSettings
    {
        public ulong ForumChannelId { get; set; }
        public ulong PendingTagChannelId { get; set; }
        public List<ulong> AllowedAssignerRoleIds { get; set; }
    }

}
