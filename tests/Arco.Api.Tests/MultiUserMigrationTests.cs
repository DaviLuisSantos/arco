using Arco.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arco.Api.Tests;

/// <summary>The <c>MultiUser</c> migration over an old single-user database, and the owner bootstrap that follows.</summary>
public sealed class MultiUserMigrationTests : IDisposable
{
    private const string BeforeMultiUser = "20261106003954_CycleFocusGoal";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arco-tests", Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public MultiUserMigrationTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "old.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private AppDbContext Open() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_path};Pooling=False").Options);

    private static void Run(AppDbContext db, string sql) => db.Database.ExecuteSqlRaw(sql);

    private static T Scalar<T>(AppDbContext db, string sql)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T));
    }

    /// <summary>A database as the single-user version left it: one habit, a check-in, XP and a custom timezone.</summary>
    private void CreateOldDatabase(bool withData)
    {
        using var db = Open();
        db.GetService<IMigrator>().Migrate(BeforeMultiUser);
        if (!withData) return;

        Run(db, "UPDATE Settings SET Timezone = 'Asia/Tokyo';");
        Run(db, """
            INSERT INTO Habits (Name, FrequencyType, FrequencyDays, CreatedAt, LevelUpWeeks, LevelUpDaysPerWeek)
            VALUES ('Ler', 'Daily', '[]', '2030-01-01 00:00:00', 2, 5);
            """);
        Run(db, "INSERT INTO ApiKeys (Name, Prefix, KeyHash, CreatedAt) VALUES ('Claude', 'arco_abc1234', 'hash-1', '2030-01-01 00:00:00');");
    }

    [Fact]
    public void An_old_database_with_data_becomes_user_1s_with_a_placeholder_email()
    {
        CreateOldDatabase(withData: true);

        using var db = Open();
        db.Database.Migrate();

        Assert.Equal(1, Scalar<int>(db, "SELECT COUNT(*) FROM Users"));
        Assert.Equal(OwnerBootstrap.PlaceholderEmail, Scalar<string>(db, "SELECT Email FROM Users WHERE Id = 1"));
        Assert.Equal(0, Scalar<int>(db, "SELECT COUNT(*) FROM Users WHERE PasswordHash IS NOT NULL"));
        Assert.Equal(0, Scalar<int>(db, "SELECT IsDemo FROM Users WHERE Id = 1"));

        // Everything that existed now belongs to user 1.
        Assert.Equal(1, Scalar<int>(db, "SELECT COUNT(*) FROM Habits WHERE UserId = 1"));
        Assert.Equal(1, Scalar<int>(db, "SELECT COUNT(*) FROM ApiKeys WHERE UserId = 1"));
        Assert.Equal(1, Scalar<int>(db, "SELECT COUNT(*) FROM Settings WHERE UserId = 1"));
        Assert.Equal("Asia/Tokyo", Scalar<string>(db, "SELECT Timezone FROM Settings WHERE UserId = 1"));
        Assert.Empty(db.Database.GetPendingMigrations());

        // The data is reachable through the query filter once acting as user 1.
        var scoped = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_path};Pooling=False").Options,
            Impersonating(1));
        Assert.Equal("Ler", scoped.Habits.Single().Name);
        Assert.Equal("Asia/Tokyo", scoped.Settings.Single().Timezone);
    }

    [Fact]
    public void An_empty_old_database_gets_no_user_and_no_orphan_settings()
    {
        CreateOldDatabase(withData: false);

        using var db = Open();
        db.Database.Migrate();

        Assert.Equal(0, Scalar<int>(db, "SELECT COUNT(*) FROM Users"));
        Assert.Equal(0, Scalar<int>(db, "SELECT COUNT(*) FROM Settings"));
    }

    [Fact]
    public void A_fresh_database_migrates_to_the_latest_with_no_users()
    {
        using var db = Open();
        db.Database.Migrate();

        Assert.Empty(db.Database.GetPendingMigrations());
        Assert.Equal(0, Scalar<int>(db, "SELECT COUNT(*) FROM Users"));
    }

    [Fact]
    public void Owner_bootstrap_claims_user_1_with_the_configured_email_and_password_once()
    {
        CreateOldDatabase(withData: true);
        using var db = Open();
        db.Database.Migrate();
        var stampBefore = Scalar<string>(db, "SELECT SecurityStamp FROM Users WHERE Id = 1");

        var hash = BCrypt.Net.BCrypt.HashPassword("senha-do-dono-1", 4);
        var config = Config(("Auth:OwnerEmail", " Dono@Exemplo.com "), ("Auth:PasswordHash", hash));
        OwnerBootstrap.Run(db, config, TimeProvider.System, NullLogger.Instance);

        using var after = Open();
        Assert.Equal("dono@exemplo.com", Scalar<string>(after, "SELECT Email FROM Users WHERE Id = 1"));
        var user = after.Users.Single();
        Assert.True(BCrypt.Net.BCrypt.Verify("senha-do-dono-1", user.PasswordHash));
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.NotEqual(stampBefore, user.SecurityStamp.ToString().ToUpperInvariant());
        Assert.Equal(1, Scalar<int>(after, "SELECT COUNT(*) FROM Habits WHERE UserId = 1"));

        // Runs once: changing the configuration later does not rewrite the account.
        var other = Config(("Auth:OwnerEmail", "outro@exemplo.com"), ("Auth:PasswordHash", BCrypt.Net.BCrypt.HashPassword("outra", 4)));
        OwnerBootstrap.Run(after, other, TimeProvider.System, NullLogger.Instance);
        using var last = Open();
        Assert.Equal("dono@exemplo.com", Scalar<string>(last, "SELECT Email FROM Users WHERE Id = 1"));
    }

    [Fact]
    public void Owner_bootstrap_leaves_the_placeholder_alone_when_the_configuration_is_missing_or_the_email_is_taken()
    {
        CreateOldDatabase(withData: true);
        using var db = Open();
        db.Database.Migrate();

        OwnerBootstrap.Run(db, Config(), TimeProvider.System, NullLogger.Instance);
        OwnerBootstrap.Run(db, Config(("Auth:OwnerEmail", "dono@exemplo.com")), TimeProvider.System, NullLogger.Instance); // no hash
        Assert.Equal(OwnerBootstrap.PlaceholderEmail, Scalar<string>(db, "SELECT Email FROM Users WHERE Id = 1"));

        Run(db, """
            INSERT INTO Users (Email, DisplayName, IsDemo, SecurityStamp, CreatedAt)
            VALUES ('dono@exemplo.com', 'Outro', 0, 'A', '2030-01-01 00:00:00');
            """);
        OwnerBootstrap.Run(db, Config(("Auth:OwnerEmail", "dono@exemplo.com"), ("Auth:PasswordHash", "x")), TimeProvider.System, NullLogger.Instance);
        Assert.Equal(OwnerBootstrap.PlaceholderEmail, Scalar<string>(db, "SELECT Email FROM Users WHERE Id = 1"));
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build();

    private static CurrentUser Impersonating(int id)
    {
        var current = new CurrentUser();
        current.Impersonate(id);
        return current;
    }
}
