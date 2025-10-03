using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Models
{
    public sealed class GuildSettings
    {
        public ulong GuildId { get; set; }
        public int SchemaVersion { get; set; } = 1;
        public string Json { get; set; } = "{}";
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public Guild Guild { get; set; } = null!;
    }
}
