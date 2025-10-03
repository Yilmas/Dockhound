using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Models
{
    public sealed class GuildSettingsHistory
    {
        public long Id { get; set; }
        public ulong GuildId { get; set; }
        public string Json { get; set; } = "{}";
        public string? ChangedBy { get; set; }
        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
