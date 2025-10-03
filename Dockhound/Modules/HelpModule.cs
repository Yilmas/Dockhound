using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Text;

namespace Dockhound.Modules
{
    public partial class DockModule
    {
        public class HelpModule : InteractionModuleBase<SocketInteractionContext>
        {
            private readonly InteractionService _interactions;
            private readonly IServiceProvider _services;

            public HelpModule(InteractionService interactions, IServiceProvider services)
            {
                _interactions = interactions;
                _services = services;
            }

            [SlashCommand("help", "List commands you can use")]
            [CommandContextType(InteractionContextType.Guild, InteractionContextType.PrivateChannel)]
            public async Task Help()
            {
                await DeferAsync(ephemeral: true);

                var user = Context.User as SocketGuildUser;
                bool inGuild = Context.Guild is not null;

                var slash = _interactions.SlashCommands;
                var ctxCmd = _interactions.ContextCommands;

                // ---------- helpers ----------
                static IEnumerable<Attribute> AllAttrs(Discord.Interactions.ModuleInfo? m)
                {
                    var cur = m;
                    while (cur != null)
                    {
                        foreach (var a in cur.Attributes) yield return a;
                        cur = cur.Parent;
                    }
                }

                static IEnumerable<GuildPermission> RequiredGuildPerms(IEnumerable<Attribute> attrs) =>
                    attrs.OfType<RequireUserPermissionAttribute>()
                         .Select(a => a.GuildPermission)
                         .Where(p => p.HasValue)
                         .Select(p => p.Value);

                static IEnumerable<ChannelPermission> RequiredChannelPerms(IEnumerable<Attribute> attrs) =>
                    attrs.OfType<RequireUserPermissionAttribute>()
                         .Select(a => a.ChannelPermission)
                         .Where(p => p.HasValue)
                         .Select(p => p.Value);

                bool HasGuildPerms(IEnumerable<GuildPermission> needed)
                {
                    if (!needed.Any()) return true;
                    if (!inGuild || user is null) return false;
                    var perms = user.GuildPermissions;
                    foreach (var p in needed)
                        if (!perms.Has(p)) return false;
                    return true;
                }

                static string SlashPath(SlashCommandInfo cmd)
                {
                    var parts = new List<string>();
                    for (var m = cmd.Module; m != null; m = m.Parent)
                        if (!string.IsNullOrWhiteSpace(m.SlashGroupName))
                            parts.Add(m.SlashGroupName);
                    parts.Reverse();
                    parts.Add(cmd.Name);
                    return "/" + string.Join(" ", parts);
                }

                // Key used for ordering inside a module (by nested slash groups)
                static string GroupPathKey(SlashCommandInfo cmd)
                {
                    var parts = new List<string>();
                    for (var m = cmd.Module; m != null; m = m.Parent)
                        if (!string.IsNullOrWhiteSpace(m.SlashGroupName))
                            parts.Add(m.SlashGroupName);
                    parts.Reverse();
                    return string.Join(" / ", parts);
                }

                static string ParamTypeLabel(SlashCommandParameterInfo p)
                {
                    var t = p.ParameterType;
                    if (t == typeof(string)) return "text";
                    if (t == typeof(bool)) return "true/false";
                    if (t == typeof(int) || t == typeof(long) || t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return "number";
                    if (typeof(IUser).IsAssignableFrom(t) || typeof(SocketUser).IsAssignableFrom(t) || typeof(SocketGuildUser).IsAssignableFrom(t)) return "user";
                    if (typeof(IChannel).IsAssignableFrom(t) || typeof(SocketGuildChannel).IsAssignableFrom(t) || typeof(SocketTextChannel).IsAssignableFrom(t) || typeof(SocketThreadChannel).IsAssignableFrom(t)) return "channel";
                    if (typeof(IRole).IsAssignableFrom(t) || typeof(SocketRole).IsAssignableFrom(t)) return "role";
                    if (typeof(IAttachment).IsAssignableFrom(t) || t.Name.IndexOf("Attachment", StringComparison.OrdinalIgnoreCase) >= 0) return "file";
                    if (t.IsEnum) return "choice";
                    return t.Name.ToLowerInvariant();
                }

                static string ParamBullet(SlashCommandParameterInfo p)
                {
                    var req = p.IsRequired ? "required" : "optional";
                    var type = ParamTypeLabel(p);
                    var desc = string.IsNullOrWhiteSpace(p.Description) ? "" : $": {p.Description}";
                    return $"• **{p.Name}** ({type}, {req}){desc}";
                }

                static string RootModuleTitle(Discord.Interactions.ModuleInfo? m)
                {
                    var root = m;
                    while (root?.Parent != null) root = root.Parent;
                    var name = root?.Name ?? "General";
                    name = name.Replace("Module", "").Replace("Interaction", "");
                    return string.IsNullOrWhiteSpace(name) ? "General" : name;
                }

                // Grouped sections: Module -> list of (GroupPathKey, CommandName, Block)
                var slashGroups = new SortedDictionary<string, List<(string GroupKey, string CommandKey, string Block)>>(StringComparer.OrdinalIgnoreCase);
                var userCtxItems = new List<string>();
                var msgCtxItems = new List<string>(); // keep as a single group if you use message commands

                void AddSlashToGroup(string moduleTitle, string groupKey, string commandKey, string block)
                {
                    if (!slashGroups.TryGetValue(moduleTitle, out var list))
                        slashGroups[moduleTitle] = list = new List<(string, string, string)>();
                    list.Add((groupKey, commandKey, block));
                }

                // ---------- Slash commands (group by root module, order by nested group path then command) ----------
                foreach (var cmd in slash)
                {
                    var needGuild = RequiredGuildPerms(cmd.Attributes)
                        .Concat(RequiredGuildPerms(AllAttrs(cmd.Module))).Distinct().ToList();
                    if (!HasGuildPerms(needGuild)) continue;

                    if (!inGuild)
                    {
                        var dmAttr = cmd.Attributes.FirstOrDefault(a => a.GetType().Name == "EnabledInDmAttribute");
                        if (dmAttr != null)
                        {
                            var prop = dmAttr.GetType().GetProperty("EnabledInDm");
                            if (prop is not null && prop.GetValue(dmAttr) is bool allowedInDm && !allowedInDm)
                                continue;
                        }
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine($"`{SlashPath(cmd)}`");
                    if (!string.IsNullOrWhiteSpace(cmd.Description))
                        sb.AppendLine(cmd.Description);

                    if (cmd.Parameters?.Count > 0)
                        foreach (var p in cmd.Parameters)
                            sb.AppendLine(ParamBullet(p));

                    var needChan = RequiredChannelPerms(cmd.Attributes)
                        .Concat(RequiredChannelPerms(AllAttrs(cmd.Module))).Distinct().ToList();
                    if (needGuild.Any() || needChan.Any())
                    {
                        var parts = new List<string>();
                        if (needGuild.Any()) parts.AddRange(needGuild.Select(x => x.ToString()));
                        if (needChan.Any()) parts.AddRange(needChan.Select(x => x + " (channel)"));
                        sb.AppendLine("Requires: " + string.Join(", ", parts.Select(s => $"`{s}`")));
                    }

                    var moduleTitle = RootModuleTitle(cmd.Module);
                    var groupKey = GroupPathKey(cmd);   // << nested path inside module
                    var commandKey = cmd.Name;
                    AddSlashToGroup(moduleTitle, groupKey, commandKey, sb.ToString().TrimEnd());
                }

                // ---------- Context commands (single groups) ----------
                foreach (var cc in ctxCmd.OrderBy(c => c.Name))
                {
                    var needGuild = RequiredGuildPerms(cc.Attributes)
                        .Concat(RequiredGuildPerms(AllAttrs(cc.Module))).Distinct().ToList();
                    if (!HasGuildPerms(needGuild)) continue;

                    var block = $"`{(cc.CommandType == ApplicationCommandType.User ? "User" : "Message")}: {cc.Name}`";

                    var needChan = RequiredChannelPerms(cc.Attributes)
                        .Concat(RequiredChannelPerms(AllAttrs(cc.Module))).Distinct().ToList();
                    if (needGuild.Any() || needChan.Any())
                    {
                        var parts = new List<string>();
                        if (needGuild.Any()) parts.AddRange(needGuild.Select(x => x.ToString()));
                        if (needChan.Any()) parts.AddRange(needChan.Select(x => x + " (channel)"));
                        block += "\n" + "Requires: " + string.Join(", ", parts.Select(s => $"`{s}`"));
                    }

                    if (cc.CommandType == ApplicationCommandType.User) userCtxItems.Add(block);
                    else if (cc.CommandType == ApplicationCommandType.Message) msgCtxItems.Add(block);
                }

                if (slashGroups.Count == 0 && userCtxItems.Count == 0 && msgCtxItems.Count == 0)
                {
                    await FollowupAsync("No commands found that you can run here.", ephemeral: true);
                    return;
                }

                // Render: inside each module, order by GroupPathKey then command name
                var sections = new List<string>();
                foreach (var (moduleTitle, items) in slashGroups)
                {
                    var ordered = items
                        .OrderBy(i => i.GroupKey, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(i => i.CommandKey, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var section = new StringBuilder();
                    section.AppendLine($"**{moduleTitle}**");
                    section.AppendLine(string.Join("\n\n", ordered.Select(i => i.Block)));
                    sections.Add(section.ToString().TrimEnd());
                }

                if (userCtxItems.Count > 0)
                    sections.Add("**User Commands**\n" + string.Join("\n\n", userCtxItems));
                if (msgCtxItems.Count > 0)
                    sections.Add("**Message Commands**\n" + string.Join("\n\n", msgCtxItems));

                var body = string.Join("\n\n", sections);
                if (body.Length > 3800) body = body[..3800] + "\n…";

                var eb = new EmbedBuilder()
                    .WithTitle("Commands you can use")
                    .WithDescription(body)
                    .WithColor(Color.Blue);

                await FollowupAsync(embed: eb.Build(), ephemeral: true);
            }
        }
    }
}
