using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Dockhound.Models;
using Dockhound.Enums;

namespace Dockhound.Logs
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
                             logEvent.EventName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             logEvent.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             logEvent.MessageId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             logEvent.UserId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             (logEvent.Changes != null && logEvent.Changes.Contains(query, StringComparison.OrdinalIgnoreCase))))
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

        public static async Task<List<LogEvent>> LookupLogEventsAsync(WllTrackerContext dbContext, string query = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            return await dbContext.Set<LogEvent>()
                .AsQueryable()
                .Where(logEvent =>
                    (string.IsNullOrWhiteSpace(query) || query.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                     logEvent.EventName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     logEvent.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     logEvent.MessageId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     logEvent.UserId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     (logEvent.Changes != null && logEvent.Changes.Contains(query, StringComparison.OrdinalIgnoreCase))) &&
                    (!startDate.HasValue || logEvent.Updated >= startDate.Value) &&
                    (!endDate.HasValue || logEvent.Updated <= endDate.Value))
                .AsNoTracking()
                .ToListAsync();
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
            using (var writer = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true))
            {
                foreach (var logEvent in events)
                {
                    string json = JsonConvert.SerializeObject(logEvent);
                    writer.WriteLine(json);
                }
                writer.Flush();
            }
            memoryStream.Position = 0; // Reset stream position
            return memoryStream;
        }

        public static async Task InsertLogEventsFromFileAsync(DbContext dbContext, string inputFilePath)
        {
            using (var reader = new StreamReader(inputFilePath))
            {
                string content = await reader.ReadToEndAsync();
                var oldEvents = JsonConvert.DeserializeObject<List<OldLogEvent>>(content);

                if (oldEvents != null && oldEvents.Any())
                {
                    var newEvents = oldEvents.Select(oldEvent => new LogEvent
                    {
                        EventName = oldEvent.Id.Split("|")[1],
                        MessageId = ulong.Parse(oldEvent.Id.Split("|")[0]),
                        Username = oldEvent.Author.Split("|")[0],
                        UserId = ulong.Parse(oldEvent.Author.Split("|")[1]),
                        Updated = oldEvent.Updated,
                        Changes = oldEvent.Changes != null ? string.Join(", ", oldEvent.Changes) : null
                    }).ToList();

                    await dbContext.Set<LogEvent>().AddRangeAsync(newEvents);
                    await dbContext.SaveChangesAsync();
                }
            }
        }
    }

    public class OldLogEvent
    {
        public string Id { get; set; }
        public string Author { get; set; }
        public DateTime Updated { get; set; }
        public List<string> Changes { get; set; }
    }

}
