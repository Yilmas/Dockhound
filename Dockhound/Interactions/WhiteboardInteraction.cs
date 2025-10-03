using Discord;
using Discord.Interactions;
using Dockhound.Components;
using Dockhound.Enums;
using Dockhound.Extensions;
using Dockhound.Modals;
using Dockhound.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dockhound.Modules.DockModule;

namespace Dockhound.Interactions
{
    public class WhiteboardInteraction : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DockhoundContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        private long seconds = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        public WhiteboardInteraction(DockhoundContext dbContext, HttpClient httpClient, IConfiguration config)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = config;
        }

        static bool CanEdit(IGuildUser user, Whiteboard wb)
        {
            // Manage Channel shortcut
            if (user.GuildPermissions.ManageChannels) return true;

            return wb.Mode switch
            {
                AccessRestriction.Restricted => false,
                AccessRestriction.Open => true,
                AccessRestriction.MembersOnly => user.RoleIds.Any(rid => wb.Roles.Any(x => x.RoleId == rid)),
                _ => false
            };
        }

        static bool CanViewHistory(IGuildUser user) =>
            user.GuildPermissions.ManageChannels; // server-side enforced


        // Button customId: wb:edit:{whiteboardId}
        [ComponentInteraction("wb:edit:*")]
        public async Task EditWhiteboardAsync(string wbIdStr)
        {
            var wbId = long.Parse(wbIdStr);

            var wb = await _dbContext.Whiteboards
                .Include(w => w.Versions)
                .FirstOrDefaultAsync(w => w.Id == wbId);

            if (wb is null)
            {
                await RespondAsync("Whiteboard not found.", ephemeral: true);
                return;
            }

            var caller = (IGuildUser)Context.User;
            if (!CanEdit(caller, wb))
            {
                await RespondAsync("You don’t have permission to edit this whiteboard.", ephemeral: true);
                return;
            }

            var latest = wb.Versions.OrderByDescending(v => v.VersionIndex).FirstOrDefault();
            var latestContent = latest?.Content ?? string.Empty;

            var modal = new ModalBuilder()
                .WithTitle("Edit Whiteboard")
                .WithCustomId($"wb:submit:{wb.Id}")
                .AddTextInput(new TextInputBuilder()
                    .WithCustomId("wb_content")               // must match your modal property’s ID
                    .WithLabel("Content")
                    .WithStyle(TextInputStyle.Paragraph)
                    .WithMaxLength(1900)
                    .WithValue(latestContent));               // prefill here

            await Context.Interaction.RespondWithModalAsync(modal.Build());
        }

        [ModalInteraction("wb:submit:*")]
        public async Task SubmitEditAsync(string wbIdStr, WhiteboardEditModal modal)
        {
            var wbId = long.Parse(wbIdStr);
            var wb = await _dbContext.Whiteboards
                .Include(w => w.Versions)
                .FirstOrDefaultAsync(w => w.Id == wbId);

            if (wb is null)
            {
                await RespondAsync("Whiteboard not found.", ephemeral: true);
                return;
            }

            var latest = wb.Versions.OrderByDescending(v => v.VersionIndex).First();
            var newContent = modal.Content ?? string.Empty;

            // TODO: your diff/percent logic here (or keep it simple for now)
            var ver = new WhiteboardVersion
            {
                WhiteboardId = wb.Id,
                VersionIndex = latest.VersionIndex + 1,
                EditorId = Context.User.Id,
                EditedUtc = DateTime.UtcNow,
                Content = newContent,
                PrevLength = latest.Content.Length,
                NewLength = newContent.Length,
                EditDistance = 0,
                PercentChanged = 0
            };

            _dbContext.WhiteboardVersions.Add(ver);
            await _dbContext.SaveChangesAsync();

            // Update the persistent message
            if (await Context.Client.GetChannelAsync(wb.ChannelId) is IMessageChannel ch
                && await ch.GetMessageAsync(wb.MessageId) is IUserMessage msg)
            {
                await msg.ModifyAsync(m => m.Content =
                    $"**{wb.Title}**\n{newContent}\n\n_WB-{wb.Id} • v{ver.VersionIndex} • Edited by <@{ver.EditorId}> • {ver.EditedUtc:yyyy-MM-dd HH:mm} UTC_");
            }

            await RespondAsync("Whiteboard updated.", ephemeral: true);
        }


        // Button customId: wb:history:{id}
        [ComponentInteraction("wb:history:*")]
        public async Task HistoryAsync(string wbIdStr)
        {
            var wbId = long.Parse(wbIdStr);
            var caller = (IGuildUser)Context.User;
            if (!caller.GuildPermissions.ManageChannels) { await RespondAsync("History requires Manage Channels.", ephemeral: true); return; }

            var versions = await _dbContext.WhiteboardVersions.Where(v => v.WhiteboardId == wbId)
                             .OrderByDescending(v => v.VersionIndex).Take(20).ToListAsync();

            var options = versions.Select(v => new SelectMenuOptionBuilder()
                .WithLabel($"#{v.VersionIndex} • {v.EditedUtc:yyyy-MM-dd HH:mm} • {v.PercentChanged}%")
                .WithValue($"wb:{wbId}|v:{v.VersionIndex}")
                .WithDescription($"by {MentionUtils.MentionUser(v.EditorId)} (+{v.NewLength - v.PrevLength} chars)")
            ).ToList();

            var menu = new SelectMenuBuilder()
                .WithCustomId($"wb:pick:{wbId}")
                .WithPlaceholder("Select a version to clone…")
                .WithType(ComponentType.SelectMenu)
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithOptions(options);

            var builder = new ComponentBuilder()
                .WithSelectMenu(menu)
                .WithButton("Clone Selected Version", $"wb:clone:{wbId}", ButtonStyle.Primary);



            await RespondAsync("Recent edits:", components: builder.Build(), ephemeral: true);
        }

        private readonly Dictionary<ulong, (long wbId, int verIndex)> _cloneSelection = new();

        // Menu selection
        [ComponentInteraction("wb:pick:*")]
        public async Task PickVersionAsync(string wbIdStr, string[] values)
        {
            if (values.Length == 0)
            {
                await RespondAsync("No version selected.", ephemeral: true);
                return;
            }

            // values[0] looks like "wb:{id}|v:{index}"
            var parts = values[0].Split('|', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2 || 
                !parts[0].StartsWith("wb:") || 
                !parts[1].StartsWith("v:"))
            {
                await RespondAsync("Invalid selection format.", ephemeral: true);
                return;
            }

            if (!long.TryParse(parts[0].Substring(3), out var wbId) ||
                !int.TryParse(parts[1].Substring(2), out var verIdx))
            {
                await RespondAsync("Could not parse version data.", ephemeral: true);
                return;
            }

            // Track selection by user
            _cloneSelection[Context.User.Id] = (wbId, verIdx);

            await RespondAsync(
                $"Selected version #{verIdx}. Click **Clone Selected Version** to create a new whiteboard.",
                ephemeral: true);
        }


        // Clone button
        [ComponentInteraction("wb:clone:*")]
        public async Task CloneAsync(string wbIdStr)
        {
            if (!_cloneSelection.TryGetValue(Context.User.Id, out var sel))
            {
                await RespondAsync("Pick a version first.", ephemeral: true); return;
            }

            var sourceWb = await _dbContext.Whiteboards.Include(w => w.Roles).FirstAsync(w => w.Id == sel.wbId);
            var sourceVer = await _dbContext.WhiteboardVersions.FirstAsync(v => v.WhiteboardId == sel.wbId && v.VersionIndex == sel.verIndex);

            var newWb = new Whiteboard
            {
                GuildId = sourceWb.GuildId,
                ChannelId = sourceWb.ChannelId,
                Title = $"{sourceWb.Title} (clone of v{sel.verIndex})",
                Mode = sourceWb.Mode,
                CreatedById = Context.User.Id,
                CreatedUtc = DateTime.UtcNow
            };
            newWb.Roles = sourceWb.Roles.Select(r => new WhiteboardRole { RoleId = r.RoleId }).ToList();

            _dbContext.Whiteboards.Add(newWb);
            await _dbContext.SaveChangesAsync();

            var seedVer = new WhiteboardVersion
            {
                WhiteboardId = newWb.Id,
                VersionIndex = 1,
                EditorId = Context.User.Id,
                EditedUtc = DateTime.UtcNow,
                Content = sourceVer.Content,
                PrevLength = 0,
                NewLength = sourceVer.Content.Length,
                EditDistance = sourceVer.Content.Length,
                PercentChanged = 100m
            };
            _dbContext.WhiteboardVersions.Add(seedVer);
            await _dbContext.SaveChangesAsync();

            // Post new message
            var content = WhiteboardComponents.BuildMessage(
                title: newWb.Title,
                content: seedVer.Content,
                wbId: newWb.Id,
                versionIndex: 1,
                editorId: Context.User.Id,
                editedUtc: seedVer.EditedUtc,
                clonedFrom: (sourceWb.Id, sel.verIndex)
            );

            var components = WhiteboardComponents.BuildComponents(newWb.Id, historyEnabled: true);

            // Post in the correct channel
            if (await Context.Client.GetChannelAsync(newWb.ChannelId) is not IMessageChannel targetCh)
            {
                await RespondAsync("Could not resolve the target channel for the cloned whiteboard.", ephemeral: true);
                return;
            }

            var msg = await targetCh.SendMessageAsync(content, components: components);
            newWb.MessageId = msg.Id;
            await _dbContext.SaveChangesAsync();


            await RespondAsync($"Created new whiteboard WB-{newWb.Id} from version #{sel.verIndex}.", ephemeral: true);
        }

        [ComponentInteraction("wb:roles:*")]
        public async Task PickRolesAsync(string messageIdStr, string[] values)
        {
            var caller = (IGuildUser)Context.User;
            if (!caller.GuildPermissions.ManageChannels)
            {
                await RespondAsync("You need **Manage Channel** to set allowed roles.", ephemeral: true);
                return;
            }

            if (!ulong.TryParse(messageIdStr, out var messageId))
            {
                await RespondAsync("Invalid message id.", ephemeral: true);
                return;
            }

            var wb = await _dbContext.Whiteboards
                .Include(w => w.Roles)
                .Include(w => w.Versions)
                .FirstOrDefaultAsync(w => w.MessageId == messageId && w.GuildId == Context.Guild.Id);

            if (wb is null)
            {
                await RespondAsync("Whiteboard not found for that message.", ephemeral: true);
                return;
            }

            if (wb.Mode != AccessRestriction.MembersOnly)
            {
                await RespondAsync("Whiteboard is not in MembersOnly mode.", ephemeral: true);
                return;
            }

            // values[] contains selected Role IDs as strings
            var roleIds = values
                .Select(v => ulong.TryParse(v, out var id) ? id : (ulong?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (roleIds.Count == 0)
            {
                await RespondAsync("No roles selected.", ephemeral: true);
                return;
            }

            // Replace allowlist
            wb.Roles.Clear();
            foreach (var rid in roleIds)
                wb.Roles.Add(new WhiteboardRole { WhiteboardId = wb.Id, RoleId = rid });

            await _dbContext.SaveChangesAsync();

            // Optionally update the on-channel message (content stays same; components unchanged)
            if (await Context.Client.GetChannelAsync(wb.ChannelId) is IMessageChannel ch &&
                await ch.GetMessageAsync(wb.MessageId) is IUserMessage msg)
            {
                var latest = wb.Versions.OrderByDescending(v => v.VersionIndex).FirstOrDefault();
                var content = WhiteboardComponents.BuildMessage(
                    wb.Title,
                    latest?.Content ?? string.Empty,
                    wb.Id,
                    latest?.VersionIndex ?? 1,
                    latest?.EditorId ?? wb.CreatedById,
                    latest?.EditedUtc ?? wb.CreatedUtc
                );
                var components = WhiteboardComponents.BuildComponents(wb.Id, historyEnabled: true);
                await msg.ModifyAsync(m => { m.Content = content; m.Components = components; });
            }

            // Confirmation (ephemeral)
            var mentions = string.Join(", ", roleIds.Select(r => MentionUtils.MentionRole(r)));
            await RespondAsync($"Allowed roles updated: {mentions}", ephemeral: true);
        }


    }
}
