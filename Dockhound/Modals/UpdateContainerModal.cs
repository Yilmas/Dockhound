using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Modals
{
    public class UpdateContainerModal : IModal
    {
        public string Title => "Update Container Count";

        [InputLabel("Red")]
        [ModalTextInput("update-count-red", maxLength: 3)]
        public string Red { get; set; }

        [InputLabel("Green")]
        [ModalTextInput("update-count-green", maxLength: 3)]
        public string Green { get; set; }

        [InputLabel("Blue")]
        [ModalTextInput("update-count-blue", maxLength: 3)]
        public string Blue { get; set; }

        [InputLabel("Dark Blue")]
        [ModalTextInput("update-count-darkblue", maxLength: 3)]
        public string DarkBlue { get; set; }

        [InputLabel("White")]
        [ModalTextInput("update-count-white", maxLength: 3)]
        public string White { get; set; }
    }
}
