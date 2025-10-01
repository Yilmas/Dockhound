using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Dockhound.Logs;

namespace Dockhound.Models;

public partial class DockhoundContext : DbContext
{
    public DbSet<LogEvent> LogEvents { get; set; } = null!;
    public DbSet<LogError> LogErrors { get; set; } = null!;

    public DockhoundContext() {}

    public DockhoundContext(DbContextOptions<DockhoundContext> options) : base(options) { }

    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //{
    //    if (!optionsBuilder.IsConfigured)
    //    {
    //        optionsBuilder.UseSqlServer("Server=10.200.1.4;User ID=wll_yilmas;Password=JyC&7BEuqaSxCpW;Database=wll_tracker;Trusted_Connection=False;TrustServerCertificate=true;");
    //    }
    //}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
