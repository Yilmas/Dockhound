using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                string json = File.ReadAllText("wll-roles-config_test.json");

                return JsonConvert.DeserializeObject<List<DiscordRoles>>(json) ?? new List<DiscordRoles>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading wll-roles-config.json: {ex.Message}");
                return new List<DiscordRoles>();
            }
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
