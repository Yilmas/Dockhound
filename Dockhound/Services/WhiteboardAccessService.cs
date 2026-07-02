using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Dockhound.Enums;
using Dockhound.Models;
using Microsoft.EntityFrameworkCore;

namespace Dockhound.Services
{
    public static class WhiteboardAccessService
    {
        public static async Task<bool> CanEditAsync(
            DockhoundContext dbContext,
            SocketInteractionContext context,
            IGuildUser user,
            long wbId)
        {
            if (context.Guild is null)
                return false;

            var wb = await dbContext.Whiteboards
                .AsNoTracking()
                .Where(w => w.Id == wbId && w.GuildId == context.Guild.Id)
                .Select(w => new { w.Mode, w.ChannelId })
                .FirstOrDefaultAsync();

            if (wb is null)
                return false;

            if (HasModeratorChannelPermissions(context, user, wb.ChannelId))
                return true;

            if (wb.Mode == AccessRestriction.Open)
                return true;

            if (wb.Mode != AccessRestriction.MembersOnly)
                return false;

            var userRoleIds = user.RoleIds.ToArray();
            return await dbContext.WhiteboardRoles
                .AnyAsync(r => r.WhiteboardId == wbId && userRoleIds.Contains(r.RoleId));
        }

        public static async Task<bool> CanModerateAsync(
            DockhoundContext dbContext,
            SocketInteractionContext context,
            IGuildUser user,
            long wbId)
        {
            if (context.Guild is null)
                return false;

            var channelId = await dbContext.Whiteboards
                .AsNoTracking()
                .Where(w => w.Id == wbId && w.GuildId == context.Guild.Id)
                .Select(w => (ulong?)w.ChannelId)
                .FirstOrDefaultAsync();

            if (channelId is null)
                return false;

            return HasModeratorChannelPermissions(context, user, channelId.Value);
        }

        private static bool HasModeratorChannelPermissions(
            SocketInteractionContext context,
            IGuildUser user,
            ulong channelId)
        {
            if (context.Guild is not SocketGuild socketGuild)
                return false;

            var channel = socketGuild.GetTextChannel(channelId);
            if (channel is null)
                return false;

            var socketUser = socketGuild.GetUser(user.Id) ?? user as SocketGuildUser;
            if (socketUser is null)
                return false;

            var permissions = socketUser.GetPermissions(channel);
            return permissions.ManageChannel || permissions.PinMessages;
        }
    }
}
