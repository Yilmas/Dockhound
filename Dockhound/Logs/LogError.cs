using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Dockhound.Enums;

namespace Dockhound.Logs
{
    [Index(nameof(Timestamp))]
    public class LogError
    {
        [Key]
        public int Id { get; set; }

        [Required] public string Message { get; set; } = string.Empty;

        [Required] public string ExceptionType { get; set; } = string.Empty;

        public string? StackTrace { get; set; }

        /// <summary>
        /// Optional: Additional debugging info
        /// </summary>
        public string? Context { get; set; }

        [Required] public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        [Required] public EnvironmentState Env { get; set; }

        public LogError() {}

        public LogError(Exception ex, string? context = null, EnvironmentState env = EnvironmentState.Development)
        {
            Message = ex.Message;
            ExceptionType = ex.GetType().FullName ?? "UnknownException";
            StackTrace = ex.StackTrace;
            Context = context;
            Timestamp = DateTime.UtcNow;
            Env = env;
        }

    }
}
