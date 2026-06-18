using Discord;
using Discord.WebSocket;

namespace Dockhound.Services
{
    public interface IHoneypotService
    {
        Task HandleMessageAsync(SocketMessage message);
        Task HandleReactionAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction);
        Task<IUserMessage> CreateHoneypotMessageAsync(SocketGuild guild, IMessageChannel channel, IUser createdBy, string? content = null);
        Task RecordSavingGraceAsync(SocketGuild guild);
    }
}
