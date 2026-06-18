using Discord;
using Discord.Interactions;

namespace Dockhound.Modules
{
    [CommandContextType(InteractionContextType.Guild)]
    [Group("dockadmin", "Root command of Dockhound Admin")]
    public partial class DockAdminModule : InteractionModuleBase<SocketInteractionContext>
    {
    }
}
