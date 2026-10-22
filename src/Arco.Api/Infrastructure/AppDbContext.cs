using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Arco.Api.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<Cycle> Cycles => Set<Cycle>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<GoalUpdate> GoalUpdates => Set<GoalUpdate>();
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<HabitLevel> HabitLevels => Set<HabitLevel>();
    public DbSet<HabitPlannedLevel> HabitPlannedLevels => Set<HabitPlannedLevel>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<XpEvent> XpEvents => Set<XpEvent>();
    public DbSet<WeeklyReview> WeeklyReviews => Set<WeeklyReview>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<PushDispatch> PushDispatches => Set<PushDispatch>();
    public DbSet<UnlockedAchievement> Achievements => Set<UnlockedAchievement>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Applies to DateTime and DateTime?: everything is stored and read back as UTC.
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Settings>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Timezone).HasMaxLength(64);
            e.HasData(new Settings { Id = Domain.Settings.SingletonId, Timezone = TimeZones.Default });
        });

        b.Entity<Cycle>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.ReviewWins).HasMaxLength(2000);
            e.Property(x => x.ReviewObstacles).HasMaxLength(2000);
            e.Property(x => x.ReviewNext).HasMaxLength(2000);
            // At most one active cycle.
            e.HasIndex(x => x.Status).IsUnique().HasFilter("\"Status\" = 'Active'");
        });

        b.Entity<Goal>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Unit).HasMaxLength(20);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Cycle).WithMany(c => c.Goals).HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Milestones).WithOne(m => m.Goal).HasForeignKey(m => m.GoalId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Updates).WithOne().HasForeignKey(u => u.GoalId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Milestone>(e => e.Property(x => x.Title).HasMaxLength(120));

        b.Entity<Habit>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.Emoji).HasMaxLength(32);
            e.Property(x => x.FrequencyType).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Goal).WithMany().HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Levels).WithOne().HasForeignKey(l => l.HabitId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.PlannedLevels).WithOne().HasForeignKey(l => l.HabitId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(x => x.CurrentLevel);
            e.Ignore(x => x.CurrentLevelNumber);
        });

        b.Entity<HabitLevel>(e =>
        {
            e.HasIndex(x => new { x.HabitId, x.Level }).IsUnique();
            e.Property(x => x.Description).HasMaxLength(120);
            e.Property(x => x.MinimumDescription).HasMaxLength(120);
        });

        b.Entity<HabitPlannedLevel>(e =>
        {
            e.HasIndex(x => new { x.HabitId, x.Position });
            e.Property(x => x.Description).HasMaxLength(120);
            e.Property(x => x.MinimumDescription).HasMaxLength(120);
        });

        b.Entity<CheckIn>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Habit).WithMany().HasForeignKey(x => x.HabitId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.HabitId, x.Date }).IsUnique();
            e.HasIndex(x => x.Date);
        });

        b.Entity<XpEvent>(e =>
        {
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Description).HasMaxLength(200);
            e.HasIndex(x => x.Date);
            e.HasOne(x => x.CheckIn).WithMany().HasForeignKey(x => x.CheckInId).OnDelete(DeleteBehavior.Cascade);
            // Earned XP survives deletion of the thing that produced it.
            e.HasOne<Habit>().WithMany().HasForeignKey(x => x.HabitId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Goal>().WithMany().HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Milestone>().WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Cycle>().WithMany().HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.WeeklyReview).WithMany().HasForeignKey(x => x.WeeklyReviewId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<WeeklyReview>(e =>
        {
            e.HasIndex(x => x.WeekStart).IsUnique();
            e.Property(x => x.Wins).HasMaxLength(2000);
            e.Property(x => x.Obstacles).HasMaxLength(2000);
            e.Property(x => x.Next).HasMaxLength(2000);
        });

        b.Entity<PushSubscription>(e =>
        {
            e.Property(x => x.Endpoint).HasMaxLength(2048);
            e.HasIndex(x => x.Endpoint).IsUnique();
            e.Property(x => x.P256dh).HasMaxLength(200);
            e.Property(x => x.Auth).HasMaxLength(100);
            e.Property(x => x.UserAgent).HasMaxLength(300);
        });

        b.Entity<PushDispatch>(e =>
        {
            e.Property(x => x.Key).HasMaxLength(100);
            e.HasIndex(x => x.Key).IsUnique();
        });

        b.Entity<ApiKey>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60);
            e.Property(x => x.Prefix).HasMaxLength(20);
            e.Property(x => x.KeyHash).HasMaxLength(64);
            e.HasIndex(x => x.KeyHash).IsUnique();
        });

        b.Entity<UnlockedAchievement>(e =>
        {
            e.Property(x => x.Key).HasMaxLength(50);
            e.HasIndex(x => x.Key).IsUnique();
        });
    }
}

public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

/// <summary>Used by dotnet-ef at design time.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=design.db").Options);
}

public static class DbErrors
{
    /// <summary>SQLITE_CONSTRAINT_UNIQUE (2067) / SQLITE_CONSTRAINT_PRIMARYKEY (1555).</summary>
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteExtendedErrorCode: 2067 or 1555 };
}
