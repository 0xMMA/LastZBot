using Microsoft.EntityFrameworkCore;

namespace LastZBot.BotService.Data;

public class LastZBotDbContext : DbContext
{
    public LastZBotDbContext(DbContextOptions<LastZBotDbContext> options)
        : base(options)
    {
    }

    public DbSet<ActionLog> ActionLogs { get; set; }
    public DbSet<Pattern> Patterns { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ActionLog>(entity =>
        {
            entity.ToTable("action_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ActionType).HasColumnName("action_type").HasMaxLength(50);
            entity.Property(e => e.MethodUsed).HasColumnName("method_used").HasMaxLength(30);
            entity.Property(e => e.Success).HasColumnName("success");
            entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
            entity.Property(e => e.CostUsd).HasColumnName("cost_usd").HasColumnType("decimal(10,6)");
            entity.Property(e => e.UiSignature).HasColumnName("ui_signature").HasMaxLength(64);
            entity.Property(e => e.ErrorType).HasColumnName("error_type").HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Pattern>(entity =>
        {
            entity.ToTable("patterns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ActionType).HasColumnName("action_type").HasMaxLength(50);
            entity.Property(e => e.Method).HasColumnName("method").HasMaxLength(30);
            entity.Property(e => e.PatternData).HasColumnName("pattern_data").HasColumnType("jsonb");
            entity.Property(e => e.SuccessCount).HasColumnName("success_count");
            entity.Property(e => e.FailureCount).HasColumnName("failure_count");
            entity.Property(e => e.LastSuccess).HasColumnName("last_success");
            entity.Property(e => e.UiSignature).HasColumnName("ui_signature").HasMaxLength(64);
        });
    }
}
