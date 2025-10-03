using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Dockhound.Logs;

namespace Dockhound.Models;

public partial class DockhoundContext : DbContext
{
    public DbSet<LogEvent> LogEvents { get; set; } = null!;
    public DbSet<LogError> LogErrors { get; set; } = null!;

    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<GuildSettings> GuildSettings => Set<GuildSettings>();
    public DbSet<GuildSettingsHistory> GuildSettingsHistories => Set<GuildSettingsHistory>();

    public DockhoundContext() {}

    public DockhoundContext(DbContextOptions<DockhoundContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OnModelCreatingPartial(modelBuilder);

        modelBuilder.Entity<Guild>(e =>
        {
            e.HasKey(x => x.GuildId);
            e.Property(x => x.GuildId).ValueGeneratedNever();
            e.HasOne(x => x.Settings)
             .WithOne(x => x.Guild)
             .HasForeignKey<GuildSettings>(x => x.GuildId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GuildSettings>(e =>
        {
            e.HasKey(x => x.GuildId);
            e.Property(x => x.SchemaVersion).IsRequired();
            e.Property(x => x.Json).IsRequired().HasColumnType("nvarchar(max)");
            e.Property(x => x.RowVersion)
             .IsRowVersion()
             .IsConcurrencyToken();
        });

        modelBuilder.Entity<GuildSettingsHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Json).IsRequired().HasColumnType("nvarchar(max)");
            e.HasIndex(x => x.GuildId);
        });
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
