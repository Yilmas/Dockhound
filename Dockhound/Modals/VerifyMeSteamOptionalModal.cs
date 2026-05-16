using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Modals
{
    public class VerifyMeSteamOptionalModal : IModal
    {
        public string Title => "Your Verification Details";

        [InputLabel("Faction")]
        [ModalSelectMenu("verify_faction", minValues: 1, maxValues: 1, Placeholder = "Select faction")]
        [ModalSelectMenuOption("Colonial", "Colonial")]
        [ModalSelectMenuOption("Warden", "Warden")]
        public string Faction { get; set; } = string.Empty;

        [InputLabel("Steam")]
        [RequiredInput(false)]
        [ModalTextInput("verify_steam", TextInputStyle.Short, placeholder: "Enter you Steam Profile Url or Steam64ID", maxLength: 200)]
        public string Steam64Id { get; set; } = string.Empty;

        [InputLabel("Upload Screenshot")]
        [ModalFileUpload("verify_file", minValues: 1, maxValues: 1)]
        public IAttachment File { get; set; } = null!;
    }
}
