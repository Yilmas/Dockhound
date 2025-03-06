using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WLL_Tracker.Models
{
    public class DiscordRoles
    {
        public string Name { get; set; }

        [JsonConverter(typeof(UlongToStringConverter))]
        public ulong Colonial { get; set; }

        [JsonConverter(typeof(UlongToStringConverter))]
        public ulong Warden { get; set; }

        [JsonConverter(typeof(UlongToStringConverter))]
        public ulong Generic { get; set; }
    }

    public static class DiscordRolesList
    {
        public static List<DiscordRoles> GetRoles()
        {
            try
            {
                string json = File.ReadAllText("roles.json");

                return JsonConvert.DeserializeObject<List<DiscordRoles>>(json) ?? new List<DiscordRoles>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading roles.json: {ex.Message}");
                return new List<DiscordRoles>();
            }
        }

        public static List<ulong> GetDeltaRoleIdList(IGuildUser user, string faction)
        {
            var rolesToAssign = new List<ulong>();

            var userRoles = user.RoleIds.ToHashSet();
                
            foreach (var role in GetRoles())
            {
                ulong roleToAssign = 0;

                if (role.Name == "Faction")
                {
                    if (faction == "Colonial")
                        roleToAssign = role.Colonial;
                    if (faction == "Warden")
                        roleToAssign = role.Warden;
                }
                else
                {
                    ulong factionRoleId = faction switch
                    {
                        "Colonial" => role.Colonial,
                        "Warden" => role.Warden,
                        _ => role.Generic
                    };

                    bool hasGenericRole = userRoles.Contains(role.Generic);

                    roleToAssign = hasGenericRole? factionRoleId : 0;
                }

                if (roleToAssign != 0)
                {
                    rolesToAssign.Add(roleToAssign);
                }
            }

            return rolesToAssign;
        }

        public static string GetDeltaRoleMentions(IGuildUser user, string faction)
        {
            List<ulong> missingRoleIds = GetDeltaRoleIdList(user, faction);

            var roleMentions = missingRoleIds.Select(roleId => $"<@&{roleId}>");

            return string.Join(", ", roleMentions);
        }
    }

    public class UlongToStringConverter : JsonConverter<ulong>
    {
        public override ulong ReadJson(JsonReader reader, Type objectType, ulong existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return ulong.TryParse(reader.Value?.ToString(), out ulong result) ? result : 0;
        }

        public override void WriteJson(JsonWriter writer, ulong value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }
    }
}
