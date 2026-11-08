namespace Arco.Api.Infrastructure;

/// <summary>
/// The <c>MultiUser</c> migration turns the data of the old single-user install into user 1, with the
/// placeholder e-mail <see cref="PlaceholderEmail"/> and no password. On startup (after <c>Migrate</c>),
/// if <c>Auth:OwnerEmail</c> and <c>Auth:PasswordHash</c> are configured, they are written to that user
/// and the e-mail is marked as verified. Runs once: afterwards the user no longer has the placeholder.
/// </summary>
public static class OwnerBootstrap
{
    public const int OwnerId = 1;
    public const string PlaceholderEmail = "owner@arco.local";

    public static void Run(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var time = scope.ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
        Run(db, app.Configuration, time, app.Logger);
    }

    public static void Run(AppDbContext db, IConfiguration config, TimeProvider time, ILogger logger)
    {
        var owner = db.Users.FirstOrDefault(u => u.Id == OwnerId && u.Email == PlaceholderEmail);
        if (owner is null) return;

        var email = config["Auth:OwnerEmail"]?.Trim().ToLowerInvariant();
        var hash = config["Auth:PasswordHash"]?.Trim();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(hash))
        {
            logger.LogWarning(
                "User {Id} still has the placeholder e-mail {Email}; set Auth:OwnerEmail and Auth:PasswordHash to claim it.",
                OwnerId, PlaceholderEmail);
            return;
        }

        if (db.Users.Any(u => u.Id != OwnerId && u.Email == email))
        {
            logger.LogError("Auth:OwnerEmail {Email} already belongs to another user; the owner was not updated.", email);
            return;
        }

        owner.Email = email;
        owner.PasswordHash = hash;
        owner.EmailVerifiedAt ??= time.GetUtcNow().UtcDateTime;
        owner.SecurityStamp = Guid.NewGuid();
        db.SaveChanges();
        logger.LogInformation("Owner account (user {Id}) set to {Email}.", OwnerId, email);
    }
}
