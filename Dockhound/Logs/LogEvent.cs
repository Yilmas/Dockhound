using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Dockhound.Enums;

namespace Dockhound.Logs
{
    [Index(nameof(GuildId), nameof(Updated))]
    [Index(nameof(GuildId), nameof(UserId), nameof(Updated))]
    [Index(nameof(GuildId), nameof(EventName), nameof(Updated))]
    [Index(nameof(GuildId), nameof(MessageId), nameof(Updated))]
    public class LogEvent
    {
        [Key]
        public int Id { get; set; }
        public ulong? GuildId { get; set; }
        [Required] public string EventName { get; set; } = string.Empty;
        [NotMapped] public LogEventType EventType => LogEventTypeExtensions.FromStoredEventName(EventName);
        public ulong MessageId { get; set; }
        [Required] public string Username { get; set; } = string.Empty;
        [Required] public ulong UserId { get; set; }
        [Required] public DateTime Updated { get; set; }
        [Required] public EnvironmentState Env { get; set; }
        public string? Changes { get; set; }

        public LogEvent() {}

        public LogEvent(LogEventType eventType, ulong? guildId, ulong messageId, string username, ulong userId, DateTime? updated = null, string? changes = null)
        {
            EventName = eventType.ToStoredValue();
            GuildId = guildId;
            MessageId = messageId;
            Username = username;
            UserId = userId;
            Updated = updated ?? DateTime.UtcNow;
            Env = GetCurrentEnvironment();
            Changes = changes;

            Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} [LOG] LogEvent, created by {Username}");
        }
        public T? GetChanges<T>()
        {
            return Changes != null ? JsonConvert.DeserializeObject<T>(Changes) : default;
        }

        private EnvironmentState GetCurrentEnvironment()
        {
            string? env = Environment.GetEnvironmentVariable("DOCK_ENVIRONMENT");

            if (string.IsNullOrEmpty(env))
            {
                return EnvironmentState.Development; // Default to Development if not set
            }

            return Enum.TryParse(env, out EnvironmentState result) ? result : EnvironmentState.Development;
        }
    }

    public enum LogEventType
    {
        Unknown = 0,
        UserBookmarkedMessage = 1,
        AllyRequest = 2,
        AllyApproved = 3,
        AllyDenied = 4,
        RecruitRequest = 5,
        RecruitApproved = 6,
        RecruitDenied = 7,
        RecruitRoleAutoAssigned = 8,
        VerificationSubmitted = 9,
        VerificationApproved = 10,
        VerificationDenied = 11,
        HoneypotBan = 12,
        YardTrackerUpdated = 13,
        WhiteboardUpdated = 14
    }

    public static class LogEventTypeExtensions
    {
        public static string ToStoredValue(this LogEventType eventType)
            => ((int)eventType).ToString(CultureInfo.InvariantCulture);

        public static string ToDisplayName(this LogEventType eventType)
            => eventType switch
            {
                LogEventType.UserBookmarkedMessage => "User Bookmarked a Message",
                LogEventType.AllyRequest => "Ally Request",
                LogEventType.AllyApproved => "Ally Approved",
                LogEventType.AllyDenied => "Ally Denied",
                LogEventType.RecruitRequest => "Recruit Request",
                LogEventType.RecruitApproved => "Recruit Approved",
                LogEventType.RecruitDenied => "Recruit Denied",
                LogEventType.RecruitRoleAutoAssigned => "Recruit Role Auto-Assigned",
                LogEventType.VerificationSubmitted => "Verification Submitted",
                LogEventType.VerificationApproved => "Verification Approved",
                LogEventType.VerificationDenied => "Verification Denied",
                LogEventType.HoneypotBan => "Honeypot Ban",
                LogEventType.YardTrackerUpdated => "Updated Yard Tracker",
                LogEventType.WhiteboardUpdated => "Updated Whiteboard",
                _ => "Unknown"
            };

        public static string[] GetStoredEventNameMatches(this LogEventType eventType)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                eventType.ToStoredValue(),
                eventType.ToString(),
                eventType.ToDisplayName()
            };

            foreach (var legacyName in eventType.GetLegacyNames())
                names.Add(legacyName);

            return names.ToArray();
        }

        public static LogEventType FromStoredEventName(string? eventName)
        {
            var normalized = eventName?.Trim();

            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var storedValue) &&
                Enum.IsDefined(typeof(LogEventType), storedValue))
            {
                return (LogEventType)storedValue;
            }

            return normalized switch
            {
                "User Bookmarked a Message" => LogEventType.UserBookmarkedMessage,
                "Ally Request" => LogEventType.AllyRequest,
                "Ally Approved" => LogEventType.AllyApproved,
                "Ally Denied" => LogEventType.AllyDenied,
                "Recruit Request" => LogEventType.RecruitRequest,
                "Recruit Approved" => LogEventType.RecruitApproved,
                "Recruit Denied" => LogEventType.RecruitDenied,
                "Recruit Role Auto-Assigned" => LogEventType.RecruitRoleAutoAssigned,
                "Verification Module" => LogEventType.VerificationSubmitted,
                "Verification Handler" => LogEventType.VerificationApproved,
                "Verification Denial" => LogEventType.VerificationDenied,
                "Honeypot Ban" => LogEventType.HoneypotBan,
                "Updated Yard Tracker" => LogEventType.YardTrackerUpdated,
                "Updated Whiteboard" => LogEventType.WhiteboardUpdated,
                _ => Enum.TryParse<LogEventType>(normalized, ignoreCase: true, out var parsed)
                    ? parsed
                    : LogEventType.Unknown
            };
        }

        private static IEnumerable<string> GetLegacyNames(this LogEventType eventType)
            => eventType switch
            {
                LogEventType.VerificationSubmitted => new[] { "Verification Module" },
                LogEventType.VerificationApproved => new[] { "Verification Handler" },
                _ => Array.Empty<string>()
            };
    }
}
