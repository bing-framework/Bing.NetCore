using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Biz.FileManagement.AspNetCore;

public sealed class FileManagementConsoleOptions
{
    /// <summary>只开启静态管理页面，实际数据仍由文件 API 验证身份和权限。</summary>
    public bool Enabled { get; set; }
}

[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("files")]
public sealed class FileManagementConsoleController : ControllerBase
{
    [HttpGet("")]
    [HttpGet("{asset}")]
    public IActionResult Resource(string? asset = null)
    {
        if (HttpContext.RequestServices.GetService<FileManagementConsoleOptions>()?.Enabled != true) return NotFound();
        asset ??= "index.html";
        var type = asset switch { "index.html" => "text/html; charset=utf-8", "console.css" => "text/css; charset=utf-8",
            "console.js" or "uploader.js" => "text/javascript; charset=utf-8", _ => null };
        if (type == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self' https: http:; img-src data:; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
        var stream = typeof(FileManagementConsoleController).Assembly.GetManifestResourceStream("Bing.Biz.FileManagement.AspNetCore.Console." + asset);
        return stream == null ? NotFound() : File(stream, type);
    }
}
