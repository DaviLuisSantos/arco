namespace Arco.Api.Infrastructure;

/// <summary>Verifies the single user's password against the BCrypt hash in Auth:PasswordHash.</summary>
public sealed class PasswordVerifier
{
    public const string DevelopmentPassword = "arco";
    private readonly string _hash;

    private PasswordVerifier(string hash, bool usingDevelopmentDefault)
    {
        _hash = hash;
        UsingDevelopmentDefault = usingDevelopmentDefault;
    }

    public bool UsingDevelopmentDefault { get; }

    public static PasswordVerifier FromConfiguration(IConfiguration config, IHostEnvironment env)
    {
        var hash = config["Auth:PasswordHash"];
        if (string.IsNullOrWhiteSpace(hash))
        {
            if (!env.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Auth:PasswordHash is not configured. Generate a BCrypt hash with " +
                    "`dotnet run --project src/Arco.Api -- hash-password <password>` and set it via the " +
                    "Auth__PasswordHash environment variable.");
            }
            return new PasswordVerifier(BCrypt.Net.BCrypt.HashPassword(DevelopmentPassword, workFactor: 4), true);
        }

        try
        {
            BCrypt.Net.BCrypt.Verify("probe", hash);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException)
        {
            throw new InvalidOperationException("Auth:PasswordHash is not a valid BCrypt hash.", ex);
        }
        return new PasswordVerifier(hash, false);
    }

    public bool Verify(string password)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, _hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
