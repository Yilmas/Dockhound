using Dockhound.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Models
{
    public class Whiteboard
    {
        public long Id { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public string Title { get; set; } = "";
        public AccessRestriction Mode { get; set; }  // enum
        public ulong CreatedById { get; set; }
        public DateTime CreatedUtc { get; set; }
        public bool IsArchived { get; set; }
        public byte[]? RowVersion { get; set; }

        public List<WhiteboardRole> Roles { get; set; } = new();
        public List<WhiteboardVersion> Versions { get; set; } = new();
    }
}
