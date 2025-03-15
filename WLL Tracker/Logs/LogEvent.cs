using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WLL_Tracker.Enums;

namespace WLL_Tracker.Logs
{
    [Index(nameof(Updated))]
    public class LogEvent
    {
        [Key]
        public int Id { get; set; }
        [Required] public string EventName { get; set; }
        public ulong MessageId { get; set; }
        [Required] public string Username { get; set; }
        [Required] public ulong UserId { get; set; }
        [Required] public DateTime Updated { get; set; }
        [Required] public EnvironmentState Env { get; set; }
        public string? Changes { get; set; }

        public LogEvent() {}

        public LogEvent(string eventName, ulong messageId, string username, ulong userId, DateTime? updated = null, string? changes = null)
        {
            EventName = eventName;
            MessageId = messageId;
            Username = username;
            UserId = userId;
            Updated = updated ?? DateTime.UtcNow;
            Env = GetCurrentEnvironment();
            Changes = changes;

            Console.WriteLine($"{DateTime.UtcNow.TimeOfDay} [LOG] LogEvent, created by {Username}");
        }
        public T? GetChanges<T>()
        {
            return Changes != null ? JsonConvert.DeserializeObject<T>(Changes) : default;
        }

        private EnvironmentState GetCurrentEnvironment()
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
