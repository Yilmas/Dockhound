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

        // Main content body (we keep ~3800 chars to stay under Discord's 4000-char message limit once headers/footers are added)
        [InputLabel("Content")]
        [ModalTextInput("wb_content", TextInputStyle.Paragraph, placeholder: "Write the whiteboard content here…", maxLength: 3800)]
        public string Content { get; set; } = string.Empty;
    }
}
