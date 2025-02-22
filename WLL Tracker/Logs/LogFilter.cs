using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace WLL_Tracker.Logs
{
    public class LogFilter
    {
        public static async Task<List<LogEvent>> LoadLogEventsAsync(string inputFilePath, string query = null)
        {
            var events = new List<LogEvent>();

            using (var reader = new StreamReader(inputFilePath))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    try
                    {
                        var logEvent = JsonConvert.DeserializeObject<LogEvent>(line);
                        if (logEvent != null &&
                            (string.IsNullOrWhiteSpace(query) || query.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                             logEvent.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             logEvent.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             logEvent.Changes.Exists(change => change.Contains(query, StringComparison.OrdinalIgnoreCase))))
                        {
                            events.Add(logEvent);
                        }
                    }
                    catch (JsonException)
                    {
                        Console.WriteLine("Skipping an invalid log entry.");
                    }
                }
            }

            return events;
        }

        public static List<LogEvent> ApplyDateRangeFilter(List<LogEvent> events, DateTime? startDate, DateTime? endDate)
        {
            return events.FindAll(log =>
                (!startDate.HasValue || log.Updated >= startDate.Value) &&
                (!endDate.HasValue || log.Updated <= endDate.Value));
        }

        public static MemoryStream ConvertToMemoryStream(List<LogEvent> events)
        {
            var memoryStream = new MemoryStream();
            var writer = new StreamWriter(memoryStream, Encoding.UTF8);

            foreach (var logEvent in events)
            {
                string json = JsonConvert.SerializeObject(logEvent);
                writer.WriteLine(json);
            }

            writer.Flush();
            memoryStream.Position = 0; // Reset stream position
            return memoryStream;
        }
    }
}
