using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
using System.Runtime.ConstrainedExecution;
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

        public WhiteboardInteraction(DockhoundContext dbContext, HttpClient httpClient, IConfiguration config)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
            _configuration = config;
        }

        private async Task<bool> CanEditAsync(IGuildUser user, long wbId)
        {
            if (user.GuildPermissions.ManageChannels) return true;

            var wb = await _dbContext.Whiteboards
                .AsNoTracking()
                .Where(w => w.Id == wbId)
                .Select(w => new { w.Mode })
                .FirstOrDefaultAsync();

            if (wb is null) return false;
            if (wb.Mode == AccessRestriction.Open) return true;
            if (wb.Mode != AccessRestriction.MembersOnly) return false;

            var userRoleIds = user.RoleIds.ToArray();
            return await _dbContext.WhiteboardRoles
                .AnyAsync(r => r.WhiteboardId == wbId && userRoleIds.Contains(r.RoleId));
        }

        [ComponentInteraction("wb:edit:*")]
        public async Task EditWhiteboardAsync(string wbIdStr)
        {
            var wbId = long.Parse(wbIdStr);

            var wb = await _dbContext.Whiteboards
                .Include(w => w.Versions)
                .FirstOrDefaultAsync(w => w.Id == wbId);

            if (wb is null)
                return;

            var caller = (IGuildUser)Context.User;
            if (!await CanEditAsync(caller, wbId))
            {
                await RespondAsync("You don’t have permission to edit this whiteboard.", ephemeral: true);
                return;
            }

            if (wb.IsArchived)
            {
                await RespondAsync("This whiteboard is archived and cannot be modified.", ephemeral: true);
                return;
            }

            var latest = wb.Versions.OrderByDescending(v => v.VersionIndex).FirstOrDefault();
            var latestContent = latest?.Content ?? string.Empty;

            var modal = new ModalBuilder()
                .WithTitle("Edit Whiteboard")
                .WithCustomId($"wb:submit:{wb.Id}")
                .AddTextInput(new TextInputBuilder()
                    .WithCustomId("wb_content")        
                    .WithLabel("Content")
                    .WithStyle(TextInputStyle.Paragraph)
                    .WithMaxLength(1900)
                    .WithValue(latestContent));   

            await Context.Interaction.RespondWithModalAsync(modal.Build());
        }

        [ModalInteraction("wb:submit:*")]
        public async Task SubmitEditAsync(string wbIdStr, WhiteboardEditModal modal)
        {
            await DeferAsync(ephemeral: true);

            if (!long.TryParse(wbIdStr, out var wbId))
            {
                await FollowupAsync("Invalid whiteboard id.", ephemeral: true);
                return;
            }

            var wb = await _dbContext.Whiteboards
                .Include(w => w.Versions)
                .FirstOrDefaultAsync(w => w.Id == wbId);

            if (wb is null)
            {
                await FollowupAsync("Whiteboard not found.", ephemeral: true);
                return;
            }

            var latest = wb.Versions.OrderByDescending(v => v.VersionIndex).First();
            var oldContent = latest.Content ?? string.Empty;
            var newContent = modal.Content ?? string.Empty;

            // If nothing changed, don’t create a new version
            if (string.Equals(oldContent, newContent, StringComparison.Ordinal))
            {
                await FollowupAsync("No changes detected.", ephemeral: true);
                return;
            }

            // Compute edit distance and % changed
            var dist = oldContent.LevenshteinDistance(newContent, ignoreCase: true);
            var maxLen = Math.Max(oldContent.Length, 1);                // avoid /0
            var percent = Math.Round((decimal)dist / maxLen * 100m, 2);

            var ver = new WhiteboardVersion
            {
                WhiteboardId = wb.Id,
                VersionIndex = latest.VersionIndex + 1,
                EditorId = Context.User.Id,
                EditedUtc = DateTime.UtcNow,
                Content = newContent,
                PrevLength = oldContent.Length,
                NewLength = newContent.Length,
                EditDistance = dist,
                PercentChanged = percent
            };

            _dbContext.WhiteboardVersions.Add(ver);
            await _dbContext.SaveChangesAsync();

            if (await Context.Client.GetChannelAsync(wb.ChannelId) is IMessageChannel ch
                && await ch.GetMessageAsync(wb.MessageId) is IUserMessage msg)
            {
                await msg.ModifyAsync(m =>
                {
                    m.Embed = WhiteboardComponents.BuildEmbed(
                        wb.Title,
                        newContent,
                        wb.Id,
                        ver.VersionIndex,
                        Context.User.Id,
                        ver.EditedUtc
                    );
                });
            }
        }


        [ComponentInteraction("wb:history:*")]
        public async Task HistoryAsync(string wbIdStr)
        {
            if (!long.TryParse(wbIdStr, out var wbId))
                return;

            var caller = (IGuildUser)Context.User;
            if (!caller.GuildPermissions.ManageChannels)
            {
                await RespondAsync("History requires **Manage Channels**.", ephemeral: true);
                return;
            }

            var wb = await _dbContext.Whiteboards
                .FirstOrDefaultAsync(w => w.Id == wbId && w.GuildId == Context.Guild.Id);

            if (wb is null)
                return;

            if (wb.IsArchived)
            {
                await RespondAsync("This whiteboard is **archived**. Unarchive it to access version history for cloning.", ephemeral: true);
                return;
            }

            var versions = await _dbContext.WhiteboardVersions
                .Where(v => v.WhiteboardId == wbId)
                .OrderByDescending(v => v.VersionIndex)
                .Take(20)
                .ToListAsync();

            if (versions.Count == 0)
            {
                await RespondAsync("No versions yet.", ephemeral: true);
                return;
            }

            var options = versions.Select(v =>
            {
                var unix = new DateTimeOffset(v.EditedUtc).ToUnixTimeSeconds();
                return new SelectMenuOptionBuilder()
                    .WithLabel($"#{v.VersionIndex} • {v.PercentChanged}% • <t:{unix}:R>")
                    .WithValue($"wb:{wbId}|v:{v.VersionIndex}")
                    .WithDescription($"by {MentionUtils.MentionUser(v.EditorId)} (+{v.NewLength - v.PrevLength} chars)");
            }).ToList();

            var menu = new SelectMenuBuilder()
                .WithCustomId($"wb:pick:{wbId}")
                .WithPlaceholder("Select a version to clone…")
                .WithType(ComponentType.SelectMenu) 
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithOptions(options);

            var components = new ComponentBuilder()
                .WithSelectMenu(menu)
                .Build();

            await RespondAsync("Select a version to clone…", components: components, ephemeral: true);
        }

        [ComponentInteraction("wb:pick:*")]
        public async Task PickVersionAsync(string wbIdStr, string[] values)
        {
            if (values.Length == 0)
            { await RespondAsync("No version selected.", ephemeral: true); return; }

            var parts = values[0].Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !parts[0].StartsWith("wb:") || !parts[1].StartsWith("v:"))
            { await RespondAsync("Invalid selection format.", ephemeral: true); return; }

            if (!long.TryParse(parts[0].Substring(3), out var wbId) ||
                !int.TryParse(parts[1].Substring(2), out var verIdx))
            { await RespondAsync("Could not parse version data.", ephemeral: true); return; }

            var comps = new ComponentBuilder()
                .WithButton("Clone Selected Version", $"wb:clone:{wbId}|v:{verIdx}", ButtonStyle.Primary)
                .WithButton("Cancel", $"wb:cancel:{wbId}", ButtonStyle.Secondary)
                .Build();

            var msgText = $"Selected version **#{verIdx}**. Click **Clone Selected Version** to create a new whiteboard.";

            if (Context.Interaction is SocketMessageComponent smc)
            {
                await smc.UpdateAsync(m =>
                {
                    m.Content = msgText;
                    m.Components = comps;
                });
            }
            else
            {
                await RespondAsync(msgText, components: comps, ephemeral: true);
            }
        }

        [ComponentInteraction("wb:cancel:*")]
        public async Task CancelCloneAsync(string wbIdStr)
        {
            if (!long.TryParse(wbIdStr, out var wbId))
            { await RespondAsync("Invalid whiteboard id.", ephemeral: true); return; }

            var caller = (IGuildUser)Context.User;
            if (!caller.GuildPermissions.ManageChannels)
            { await RespondAsync("History requires **Manage Channels**.", ephemeral: true); return; }

            var versions = await _dbContext.WhiteboardVersions
                .Where(v => v.WhiteboardId == wbId)
                .OrderByDescending(v => v.VersionIndex)
                .Take(20)
                .ToListAsync();

            if (versions.Count == 0)
            { await RespondAsync("No versions yet.", ephemeral: true); return; }

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

            var comps = new ComponentBuilder()
                .WithSelectMenu(menu) 
                .Build();

            if (Context.Interaction is SocketMessageComponent smc)
            {
                await smc.UpdateAsync(m =>
                {
                    m.Content = "Select a version to clone…";
                    m.Components = comps;
                });
            }
            else
            {
                await RespondAsync("Select a version to clone…", components: comps, ephemeral: true);
            }
        }

        [ComponentInteraction("wb:clone:*")]
        public async Task CloneAsync(string payload)
        {
            var parts = payload.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !parts[1].StartsWith("v:") || !long.TryParse(parts[0], out var wbId) ||
                !int.TryParse(parts[1].Substring(2), out var verIdx))
            {
                await RespondAsync("Pick a version first.", ephemeral: true);
                return;
            }

            var sourceWb = await _dbContext.Whiteboards.Include(w => w.Roles).FirstOrDefaultAsync(w => w.Id == wbId);
            if (sourceWb is null)
            {
                await RespondAsync("Source whiteboard not found.", ephemeral: true);
                return;
            }

            var sourceVer = await _dbContext.WhiteboardVersions
                .FirstOrDefaultAsync(v => v.WhiteboardId == wbId && v.VersionIndex == verIdx);

            if (sourceVer is null)
            {
                await RespondAsync("Selected version not found.", ephemeral: true);
                return;
            }

            var newWb = new Whiteboard
            {
                GuildId = sourceWb.GuildId,
                ChannelId = sourceWb.ChannelId,
                Title = sourceWb.Title,
                Mode = sourceWb.Mode,
                CreatedById = Context.User.Id,
                CreatedUtc = DateTime.UtcNow,
                Roles = sourceWb.Roles.Select(r => new WhiteboardRole { RoleId = r.RoleId }).ToList()
            };

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

            var embed = WhiteboardComponents.BuildEmbed(
                newWb.Title,
                seedVer.Content,
                newWb.Id,
                1,
                Context.User.Id,
                newWb.CreatedUtc,
                clonedFrom: (sourceWb.Id, verIdx)
            );

            var components = WhiteboardComponents.BuildComponents(newWb.Id, historyEnabled: true);

            if (await Context.Client.GetChannelAsync(newWb.ChannelId) is not IMessageChannel targetCh)
            {
                await RespondAsync("Could not resolve the target channel for the cloned whiteboard.", ephemeral: true);
                return;
            }

            var msg = await targetCh.SendMessageAsync(embed: embed, components: components);
            newWb.MessageId = msg.Id;
            await _dbContext.SaveChangesAsync();

            await RespondAsync($"Created new whiteboard for \"{newWb.Title}\" from version **#{verIdx}**.", ephemeral: true);
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
                return;

            var wb = await _dbContext.Whiteboards
                .Include(w => w.Roles)
                .Include(w => w.Versions)
                .FirstOrDefaultAsync(w => w.MessageId == messageId && w.GuildId == Context.Guild.Id);

            if (wb is null)
                return;

            if (wb.Mode != AccessRestriction.MembersOnly)
            {
                await RespondAsync("Whiteboard is not in MembersOnly mode.", ephemeral: true);
                return;
            }

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

            wb.Roles.Clear();
            foreach (var rid in roleIds)
                wb.Roles.Add(new WhiteboardRole { WhiteboardId = wb.Id, RoleId = rid });

            await _dbContext.SaveChangesAsync();

            if (await Context.Client.GetChannelAsync(wb.ChannelId) is IMessageChannel ch &&
                await ch.GetMessageAsync(wb.MessageId) is IUserMessage msg)
            {
                var latest = wb.Versions.OrderByDescending(v => v.VersionIndex).FirstOrDefault();

                await msg.ModifyAsync(m =>
                {
                    m.Embed = WhiteboardComponents.BuildEmbed(
                        wb.Title,
                        latest?.Content ?? string.Empty,
                        wb.Id,
                        latest?.VersionIndex ?? 1,
                        latest?.EditorId ?? wb.CreatedById,
                        latest?.EditedUtc ?? wb.CreatedUtc
                    );
                });
            }

            var mentions = string.Join(", ", roleIds.Select(r => MentionUtils.MentionRole(r)));
            await RespondAsync($"Allowed roles updated: {mentions}", ephemeral: true);
        }


    }
}
