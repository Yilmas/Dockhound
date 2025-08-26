using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Modals
{
    public class DenyReasonModal : IModal
    {
        public string Title => "Denial Reason";

        [InputLabel("Why are you denying this?")]
        [ModalTextInput("deny-reason-text", TextInputStyle.Paragraph, maxLength: 500, placeholder: "Enter the reason for denial...")]
        public string Reason { get; set; } = string.Empty;
    }
}
