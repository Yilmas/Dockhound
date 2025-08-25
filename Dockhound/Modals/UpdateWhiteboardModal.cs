using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Modals
{
    public class UpdateWhiteboardModal : IModal
    {
        public string Title => "Add Message";

        [InputLabel("Message")]
        [ModalTextInput("update-whiteboard-edit", TextInputStyle.Paragraph, maxLength: 2048)]
        public string Message { get; set; } = string.Empty;
    }
}
