using System.Net;

namespace Arco.Api.Infrastructure.Email;

/// <summary>Transactional e-mails in the account's language (<c>User.Locale</c>: "pt-BR", the default, or "en").</summary>
public static class EmailTemplates
{
    public static (string subject, string html) VerifyEmail(string link, string? locale = null) =>
        IsEnglish(locale)
            ? ("Confirm your e-mail on Arco",
               Layout("en", "Confirm your e-mail", "Welcome to Arco! Confirm your e-mail to continue. The link is valid for 48 hours.",
                   "Confirm e-mail", link))
            : ("Confirme seu e-mail no Arco",
               Layout("pt-BR", "Confirme seu e-mail", "Bem-vindo ao Arco! Confirme seu e-mail para continuar. O link vale por 48 horas.",
                   "Confirmar e-mail", link));

    public static (string subject, string html) ResetPassword(string link, string? locale = null) =>
        IsEnglish(locale)
            ? ("Reset your Arco password",
               Layout("en", "Reset password",
                   "We received a request to reset your password. The link is valid for 1 hour. If it wasn't you, ignore this e-mail.",
                   "Reset password", link))
            : ("Redefinir sua senha do Arco",
               Layout("pt-BR", "Redefinir senha",
                   "Recebemos um pedido para redefinir sua senha. O link vale por 1 hora. Se não foi você, ignore este e-mail.",
                   "Redefinir senha", link));

    private static bool IsEnglish(string? locale) => locale?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;

    private static string Layout(string lang, string title, string text, string button, string link)
    {
        var l = WebUtility.HtmlEncode(link);
        var copy = lang == "en" ? "Or copy the address" : "Ou copie o endereço";
        return $"""
<div lang="{lang}" style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#1c1917">
  <h1 style="font-size:20px;margin:0 0 16px">Arco</h1>
  <h2 style="font-size:18px;margin:0 0 12px">{title}</h2>
  <p style="line-height:1.5">{text}</p>
  <p style="margin:24px 0"><a href="{l}" style="background:#1c1917;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none">{button}</a></p>
  <p style="font-size:12px;color:#78716c;word-break:break-all">{copy}: {l}</p>
</div>
""";
    }
}
