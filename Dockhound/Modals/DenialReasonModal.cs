using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

namespace Dockhound.Modals
{
    /// <summary>
    /// NOT IN USE, WILL NEED REDESIGN OF INTERACTION HANDLER
    /// </summary>
    public class DenialReasonModal : IModal
    {
        public string Title => "Denial Reason";

        [InputLabel("Why are you denying this?")]
        [ModalTextInput("deny_reason_text", TextInputStyle.Paragraph, "Enter the reason for denial...", maxLength: 500)]
        public string Reason { get; set; } = string.Empty;
    }
}
