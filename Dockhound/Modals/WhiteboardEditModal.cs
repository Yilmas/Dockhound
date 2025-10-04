using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Modals
{
    public class WhiteboardEditModal : IModal
    {
        // The modal window title
        public string Title => "Edit Whiteboard";

        // Main content body (we keep ~1900 chars to stay under Discord's 2000-char message limit once headers/footers are added)
        [InputLabel("Content")]
        [ModalTextInput("wb_content", TextInputStyle.Paragraph, placeholder: "Write the whiteboard content here…", maxLength: 1900)]
        public string Content { get; set; } = string.Empty;
    }
}
