using System.Net;

namespace Arco.Api.Infrastructure.Email;

public static class EmailTemplates
{
    public static (string subject, string html) VerifyEmail(string link) =>
        ("Confirme seu e-mail no Arco",
         Layout("Confirme seu e-mail", "Bem-vindo ao Arco! Confirme seu e-mail para continuar. O link vale por 48 horas.", "Confirmar e-mail", link));

    public static (string subject, string html) ResetPassword(string link) =>
        ("Redefinir sua senha do Arco",
         Layout("Redefinir senha", "Recebemos um pedido para redefinir sua senha. O link vale por 1 hora. Se não foi você, ignore este e-mail.", "Redefinir senha", link));

    private static string Layout(string title, string text, string button, string link)
    {
        var l = WebUtility.HtmlEncode(link);
        return $"""
<div style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#1c1917">
  <h1 style="font-size:20px;margin:0 0 16px">Arco</h1>
  <h2 style="font-size:18px;margin:0 0 12px">{title}</h2>
  <p style="line-height:1.5">{text}</p>
  <p style="margin:24px 0"><a href="{l}" style="background:#1c1917;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none">{button}</a></p>
  <p style="font-size:12px;color:#78716c;word-break:break-all">Ou copie o endereço: {l}</p>
</div>
""";
    }
}
