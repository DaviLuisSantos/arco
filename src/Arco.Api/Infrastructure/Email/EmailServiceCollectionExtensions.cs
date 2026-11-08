namespace Arco.Api.Infrastructure.Email;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddArcoEmail(this IServiceCollection s, IConfiguration c)
    {
        if (!string.IsNullOrWhiteSpace(c["Email:ResendApiKey"]))
            s.AddHttpClient<IEmailSender, ResendEmailSender>();
        else
            s.AddSingleton<IEmailSender, LogEmailSender>();
        return s;
    }
}
