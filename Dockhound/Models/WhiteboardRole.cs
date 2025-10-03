using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Models
{
    public class WhiteboardRole
    {
        public long WhiteboardId { get; set; }
        public Whiteboard Whiteboard { get; set; } = default!;
        public ulong RoleId { get; set; }
    }
}
