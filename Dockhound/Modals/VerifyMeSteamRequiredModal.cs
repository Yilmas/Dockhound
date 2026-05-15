using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Modals
{
    public class VerifyMeSteamRequiredModal : IModal
    {
        public string Title => "Verification";

        [InputLabel("Faction")]
        [ModalSelectMenu("verify_faction", minValues: 1, maxValues: 1, Placeholder = "Select faction")]
        [ModalSelectMenuOption("Colonial", "Colonial")]
        [ModalSelectMenuOption("Warden", "Warden")]
        public string Faction { get; set; } = string.Empty;

        [InputLabel("Steam")]
        [RequiredInput(true)]
        [ModalTextInput("verify_steam", TextInputStyle.Short, placeholder: "Enter you Steam Profile Url, ID, or 64Id", maxLength: 200)]
        public string Steam64Id { get; set; } = string.Empty;

        [InputLabel("Upload Screenshot")]
        [ModalFileUpload("verify_file", minValues: 1, maxValues: 1)]
        public IAttachment File { get; set; } = null!;
    }
}
