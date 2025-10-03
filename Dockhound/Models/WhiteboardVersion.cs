using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Models
{
    public class WhiteboardVersion
    {
        public long Id { get; set; }
        public long WhiteboardId { get; set; }
        public Whiteboard Whiteboard { get; set; } = default!;
        public int VersionIndex { get; set; }
        public ulong EditorId { get; set; }
        public DateTime EditedUtc { get; set; }
        public string Content { get; set; } = "";

        public int PrevLength { get; set; }
        public int NewLength { get; set; }
        public int EditDistance { get; set; }
        public decimal PercentChanged { get; set; } // 5,2
    }
}
