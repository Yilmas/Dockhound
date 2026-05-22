using Discord;
using Discord.Interactions;
using Dockhound.Components;
using Dockhound.Enums;
using Dockhound.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dockhound.Modules
{
    public partial class DockModule
    {
        [Group("whiteboard", "Manage collaborative whiteboards")]
        public class WhiteboardModule : InteractionModuleBase<SocketInteractionContext>
        {
            private readonly DockhoundContext _db;

            public WhiteboardModule(DockhoundContext db)
            {
                _db = db;
            }

            [SlashCommand("create", "Create a new whiteboard")]
            public async Task CreateAsync(string title, AccessRestriction mode = AccessRestriction.Open)
            {
                await DeferAsync(ephemeral: true);

                var wb = new Whiteboard
                {
                    GuildId = Context.Guild.Id,
                    ChannelId = Context.Channel.Id,
                    Title = title,
                    Mode = mode,
                    CreatedById = Context.User.Id,
                    CreatedUtc = DateTime.UtcNow,
                    IsArchived = false
                };

                // Seed with empty content
                wb.Versions.Add(new WhiteboardVersion
                {
                    VersionIndex = 1,
                    EditorId = Context.User.Id,
                    EditedUtc = DateTime.UtcNow,
                    Content = string.Empty,
                    PrevLength = 0,
                    NewLength = 0,
                    EditDistance = 0,
                    PercentChanged = 0
                });

                _db.Whiteboards.Add(wb);
                await _db.SaveChangesAsync();

                var embed = WhiteboardComponents.BuildEmbed(
                    wb.Title,
                    string.Empty,
                    wb.Id,
                    1,
                    Context.User.Id,
                    wb.CreatedUtc
                );

                var msg = await Context.Channel.SendMessageAsync(embed: embed,
                    components: WhiteboardComponents.BuildComponents(wb.Id, historyEnabled: true));
                wb.MessageId = msg.Id;
                await _db.SaveChangesAsync();

                await FollowupAsync($"Whiteboard **{wb.Title}** created.", ephemeral: true);

                if (mode == AccessRestriction.MembersOnly)
                {
                    var caller = (IGuildUser)Context.User;
                    if (!caller.GuildPermissions.ManageChannels)
                    {
                        await FollowupAsync("Whiteboard is in **MembersOnly** mode, but you need **Manage Channels** to set allowed roles.", ephemeral: true);
                        return;
                    }

                    var roleMenu = new SelectMenuBuilder()
                        .WithCustomId($"wb:roles:{wb.MessageId}") 
                        .WithPlaceholder("Select allowed roles…")
                        .WithType(ComponentType.RoleSelect)
                        .WithMinValues(1)
                        .WithMaxValues(10);

                    await FollowupAsync(
                        "Pick one or more roles allowed to edit this whiteboard:",
                        components: new ComponentBuilder().WithSelectMenu(roleMenu).Build(),
                        ephemeral: true);
                }
            }

            [SlashCommand("mode", "Change the privacy mode of a whiteboard")]
            public async Task ModeAsync(
            [Summary(description: "Whiteboard message ID")] string messageId,
            AccessRestriction mode)
            {
                ulong.TryParse(messageId, out ulong messageIdUlong);

                var caller = (IGuildUser)Context.User;
                if (!caller.GuildPermissions.ManageChannels)
                {
                    await RespondAsync("You need **Manage Channel** to change whiteboard mode.", ephemeral: true);
                    return;
                }

                var wb = await _db.Whiteboards
                    .Include(w => w.Roles)
                    .Include(w => w.Versions)
                    .FirstOrDefaultAsync(w => w.MessageId == messageIdUlong && w.GuildId == Context.Guild.Id);

                if (wb is null)
                {
                    await RespondAsync("Whiteboard not found for that message.", ephemeral: true);
                    return;
                }

                wb.Mode = mode;

                // If NOT MembersOnly: clear allowlist and finish.
                if (mode != AccessRestriction.MembersOnly)
                {
                    wb.Roles.Clear();
                    await _db.SaveChangesAsync();

                    await RespondAsync($"Mode for message `{messageIdUlong}` set to **{mode}**.", ephemeral: true);
                    return;
                }

                await _db.SaveChangesAsync(); 

                var roleMenu = new SelectMenuBuilder()
                    .WithCustomId($"wb:roles:{messageIdUlong}")
                    .WithPlaceholder("Select allowed roles…")
                    .WithType(ComponentType.RoleSelect)
                    .WithMinValues(1)
                    .WithMaxValues(10);

                var controls = new ComponentBuilder().WithSelectMenu(roleMenu);

                await RespondAsync(
                    $"Mode set to **MembersOnly**. Pick one or more roles allowed to edit:",
                    components: controls.Build(),
                    ephemeral: true);
            }

            [SlashCommand("roles", "Set or update allowed roles for a MembersOnly whiteboard")]
            public async Task RolesAsync([Summary(description: "Whiteboard message ID")] string messageId)
            {
                ulong.TryParse(messageId, out ulong messageIdUlong);

                var caller = (IGuildUser)Context.User;
                if (!caller.GuildPermissions.ManageChannels)
                {
                    await RespondAsync("You need **Manage Channel** to set allowed roles.", ephemeral: true);
                    return;
                }

                var wb = await _db.Whiteboards
                    .FirstOrDefaultAsync(w => w.MessageId == messageIdUlong && w.GuildId == Context.Guild.Id);

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

                var roleMenu = new SelectMenuBuilder()
                    .WithCustomId($"wb:roles:{messageId}")
                    .WithPlaceholder("Select allowed roles…")
                    .WithType(ComponentType.RoleSelect)
                    .WithMinValues(1)
                    .WithMaxValues(10);

                await RespondAsync("Pick one or more roles allowed to edit:", components: new ComponentBuilder().WithSelectMenu(roleMenu).Build(), ephemeral: true);
            }


            [SlashCommand("info", "Show details about a whiteboard")]
            public async Task InfoAsync(
                [Summary(description: "Whiteboard message ID")] string messageId)
            {
                ulong.TryParse(messageId, out ulong messageIdUlong);

                var wb = await _db.Whiteboards
                    .Include(w => w.Roles)
                    .Include(w => w.Versions)
                    .FirstOrDefaultAsync(w => w.MessageId == messageIdUlong && w.GuildId == Context.Guild.Id);

                if (wb is null)
                {
                    await RespondAsync("Whiteboard not found.", ephemeral: true);
                    return;
                }

                var latest = wb.Versions.OrderByDescending(v => v.VersionIndex).FirstOrDefault();
                var createdByMention = MentionUtils.MentionUser(wb.CreatedById);
                var channelMention = MentionUtils.MentionChannel(wb.ChannelId);
                var roles = wb.Roles?.Select(r => MentionUtils.MentionRole(r.RoleId)).ToList() ?? [];

                var eb = new EmbedBuilder()
                    .WithTitle($"Whiteboard Info — WB-{wb.Id}")
                    .WithColor(Color.Teal)
                    .AddField("Title", wb.Title, true)
                    .AddField("Channel", channelMention, true)
                    .AddField("Mode", wb.Mode.ToString(), true)
                    .AddField("Allowed Roles",
                        wb.Mode == AccessRestriction.MembersOnly
                            ? (roles.Count > 0 ? string.Join(", ", roles) : "_(none)_")
                            : "_(not applicable)_",
                        false)
                    .AddField("Created",
                        $"{createdByMention} • {TimestampTag.FromDateTime(wb.CreatedUtc, TimestampTagStyles.ShortDateTime)}",
                        true)
                    .AddField("Archived", wb.IsArchived ? "Yes" : "No", true)
                    .AddField("Latest Version",
                        latest is null
                            ? "_none_"
                            : $"v{latest.VersionIndex} • by {MentionUtils.MentionUser(latest.EditorId)} • {TimestampTag.FromDateTime(latest.EditedUtc, TimestampTagStyles.ShortDateTime)}",
                        false)
                    .WithFooter($"WhiteboardId: {wb.Id} • MessageId: {wb.MessageId}");

                var caller = (IGuildUser)Context.User;
                if (caller.GuildPermissions.ManageChannels)
                {
                    var last5 = wb.Versions
                        .OrderByDescending(v => v.VersionIndex)
                        .Take(5)
                        .Select(v =>
                        {
                            var unix = new DateTimeOffset(v.EditedUtc).ToUnixTimeSeconds();
                            return $"v{v.VersionIndex}: {v.PercentChanged}% by <@{v.EditorId}> (<t:{unix}:R>)";
                        })
                        .ToList();

                    if (last5.Count > 0)
                        eb.AddField("Recent Edits (last 5)", string.Join("\n", last5), false);
                }

                await RespondAsync(embed: eb.Build(), ephemeral: true);
            }

            [SlashCommand("archive", "Toggle archive on a whiteboard (close/open)")]
            public async Task ArchiveAsync([Summary(description: "Whiteboard message ID")] string messageId)
            {
                ulong.TryParse(messageId, out ulong messageIdUlong);

                var caller = (IGuildUser)Context.User;
                if (!caller.GuildPermissions.ManageChannels)
                {
                    await RespondAsync("You need **Manage Channels** to archive/unarchive a whiteboard.", ephemeral: true);
                    return;
                }

                var wb = await _db.Whiteboards
                    .Include(w => w.Versions)
                    .FirstOrDefaultAsync(w => w.MessageId == messageIdUlong && w.GuildId == Context.Guild.Id);

                if (wb is null)
                {
                    await RespondAsync("Whiteboard not found for that message.", ephemeral: true);
                    return;
                }

                wb.IsArchived = !wb.IsArchived;

                string title = wb.Title;
                const string archivedTag = "🔒 ";
                if (wb.IsArchived)
                {
                    if (!title.StartsWith(archivedTag, StringComparison.OrdinalIgnoreCase))
                        title = archivedTag + title;
                }
                else
                {
                    if (title.StartsWith(archivedTag, StringComparison.OrdinalIgnoreCase))
                        title = title.Substring(archivedTag.Length);
                }
                wb.Title = title;

                await _db.SaveChangesAsync();

                // Rebuild embed with current content + new title
                var latest = wb.Versions.OrderByDescending(v => v.VersionIndex).FirstOrDefault();
                var embed = WhiteboardComponents.BuildEmbed(
                    title: wb.Title,
                    content: latest?.Content ?? string.Empty,
                    wbId: wb.Id,
                    versionIndex: latest?.VersionIndex ?? 1,
                    editorId: latest?.EditorId ?? wb.CreatedById,
                    editedUtc: latest?.EditedUtc ?? wb.CreatedUtc
                );

                // Update the original message: clear or restore components
                if (await Context.Client.GetChannelAsync(wb.ChannelId) is IMessageChannel ch &&
                    await ch.GetMessageAsync(wb.MessageId) is IUserMessage msg)
                {
                    await msg.ModifyAsync(m =>
                    {
                        m.Embed = embed;
                        m.Components = wb.IsArchived
                            ? new ComponentBuilder().Build() // remove buttons
                            : WhiteboardComponents.BuildComponents(wb.Id, historyEnabled: true);
                    });
                }

                await RespondAsync(
                    wb.IsArchived
                        ? $"Whiteboard **{wb.Title}** archived."
                        : $"Whiteboard **{wb.Title}** unarchived.",
                    ephemeral: true);
            }

            [SlashCommand("list", "List all whiteboards in the current channel")]
            public async Task ListAsync()
            {
                await DeferAsync(ephemeral: true);

                if (Context.Guild is null)
                {
                    await FollowupAsync("This command must be used inside a guild.", ephemeral: true);
                    return;
                }

                var channelId = Context.Channel.Id;

                var whiteboards = await _db.Whiteboards
                    .Include(w => w.Versions)
                    .Where(w => w.GuildId == Context.Guild.Id && w.ChannelId == channelId)
                    .OrderBy(w => w.Id)
                    .ToListAsync();

                if (!whiteboards.Any())
                {
                    await FollowupAsync("No whiteboards found in this channel.", ephemeral: true);
                    return;
                }

                // Verify messages still exist for whiteboards that reference a MessageId.
                // If a referenced message or channel no longer exists, set IsDeleted = true (persist the change)
                // so the list only shows whiteboards with IsDeleted == false.
                var anyChanges = false;

                foreach (var wb in whiteboards)
                {
                    if (wb.IsDeleted)
                        continue; // already marked deleted, skip verification

                    if (wb.MessageId == 0)
                    {
                        // No message associated -> treat as deleted/missing for listing purposes.
                        wb.IsDeleted = true;
                        anyChanges = true;
                        continue;
                    }

                    try
                    {
                        var ch = await Context.Client.GetChannelAsync(wb.ChannelId);
                        if (ch is not IMessageChannel msgCh)
                        {
                            wb.IsDeleted = true;
                            anyChanges = true;
                            continue;
                        }

                        var msg = await msgCh.GetMessageAsync(wb.MessageId);
                        if (msg is null)
                        {
                            // Message was deleted -> mark whiteboard as deleted
                            wb.IsDeleted = true;
                            anyChanges = true;
                        }
                    }
                    catch
                    {
                        // On error (permissions, network, etc.) assume message is gone and mark deleted.
                        if (!wb.IsDeleted)
                        {
                            wb.IsDeleted = true;
                            anyChanges = true;
                        }
                    }
                }

                if (anyChanges)
                    await _db.SaveChangesAsync();

                // Only show whiteboards that are not marked deleted
                var toShow = whiteboards.Where(wb => !wb.IsDeleted).ToList();

                if (!toShow.Any())
                {
                    await FollowupAsync("No whiteboards found in this channel.", ephemeral: true);
                    return;
                }

                var sb = new System.Text.StringBuilder();

                foreach (var wb in toShow)
                {
                    var latest = wb.Versions?.OrderByDescending(v => v.VersionIndex).FirstOrDefault();
                    var title = string.IsNullOrWhiteSpace(wb.Title) ? $"WB-{wb.Id}" : wb.Title.Replace("[", "\\[").Replace("]", "\\]");

                    string entry;
                    if (wb.MessageId != 0)
                    {
                        var url = $"https://discord.com/channels/{Context.Guild.Id}/{wb.ChannelId}/{wb.MessageId}";
                        entry = $"• [{title}]({url}) — WB-{wb.Id} — v{latest?.VersionIndex ?? 0} by <@{latest?.EditorId ?? wb.CreatedById}>{(wb.IsArchived ? " 🔒" : "")}";
                    }
                    else
                    {
                        entry = $"• {title} — WB-{wb.Id} — (no message) — v{latest?.VersionIndex ?? 0} by <@{latest?.EditorId ?? wb.CreatedById}>{(wb.IsArchived ? " 🔒" : "")}";
                    }

                    sb.AppendLine(entry);
                }

                var content = sb.ToString();

                // If too long for a message, send as a file
                if (content.Length > 1900)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                    await using var ms = new System.IO.MemoryStream(bytes);
                    await FollowupWithFileAsync(ms, $"whiteboards-{channelId}.txt", text: $"Whiteboards in {MentionUtils.MentionChannel(channelId)}:", ephemeral: true);
                }
                else
                {
                    await FollowupAsync($"Whiteboards in {MentionUtils.MentionChannel(channelId)}:\n{content}", ephemeral: true);
                }
            }
        }
    }
        
}
