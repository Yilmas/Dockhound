using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dockhound.Models;

namespace Dockhound.Logs
{
    public static class ExceptionHandler
    {
        public static async Task HandleExceptionAsync(Exception ex, DockhoundContext dbContext, string? context = null)
        {
            try
            {
                // Log to Console
                Console.WriteLine($"[ERROR] {DateTime.UtcNow}: {ex.Message}");

                // Log to DB
                var errorLog = new LogError(ex, context);
                dbContext.LogErrors.Add(errorLog);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception dbEx)
            {
                // If DB fail, print to Console
                Console.WriteLine($"[CRITICAL ERROR] Failed to log exception to database: {dbEx.Message}");
                Console.WriteLine($"[STACK TRACE] {dbEx.StackTrace}");
            }
        }
    }
}
