using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Bing.Resilience.Operations.AspNetCore;

internal static class OperationsConsole
{
    private static readonly string Page = LoadPage();
    private static string LoadPage()
    {
        using var stream = typeof(OperationsConsole).Assembly.GetManifestResourceStream("Bing.Resilience.Operations.Console.html")!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
    internal static IResult Render(HttpContext context, ResilienceOperationsEndpointOptions options)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        context.Response.Headers["Content-Security-Policy"] = $"default-src 'none'; script-src 'nonce-{nonce}'; style-src 'nonce-{nonce}'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        var pathBase = context.Request.PathBase.Value ?? "";
        return Results.Content(Page.Replace("{{nonce}}", nonce, StringComparison.Ordinal)
            .Replace("{{replay}}", System.Net.WebUtility.HtmlEncode(pathBase + options.ReplayPrefix), StringComparison.Ordinal)
            .Replace("{{saga}}", System.Net.WebUtility.HtmlEncode(pathBase + options.SagaPrefix), StringComparison.Ordinal)
            .Replace("{{session}}", System.Net.WebUtility.HtmlEncode(pathBase + options.ConsolePath + "/session"), StringComparison.Ordinal), "text/html", Encoding.UTF8);
    }
}
