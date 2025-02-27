using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using WLL_Tracker.Logs;

namespace WLL_Tracker.Models;

public partial class WllTrackerContext : DbContext
{
    public DbSet<LogEvent> LogEvents { get; set; } = null!;
    public DbSet<LogError> LogErrors { get; set; } = null!;

    public WllTrackerContext() {}

    public WllTrackerContext(DbContextOptions<WllTrackerContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
