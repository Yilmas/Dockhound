using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Models
{
    public sealed class Guild
    {
        public ulong GuildId { get; set; }
        public string? Name { get; set; }
        public DateTime? CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public GuildSettings? Settings { get; set; }
    }
}
