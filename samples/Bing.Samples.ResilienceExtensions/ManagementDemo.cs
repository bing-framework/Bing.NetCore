using System.Data.Common;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bing.Resilience;
using Bing.Resilience.Compensation;
using Bing.Resilience.Operations;
using Bing.Resilience.Operations.AspNetCore;
using Bing.Resilience.Operations.Relational;
using Bing.Resilience.Replay;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;

/// <summary>仅绑定 loopback 的演示管理宿主；真实应用应替换成自身的认证与权限体系。</summary>
internal static class ManagementDemo
{
    public static async Task Run()
    {
        var password = Environment.GetEnvironmentVariable("BING_RESILIENCE_DEMO_TOKEN");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 16)
            throw new InvalidOperationException("请先设置至少 16 字符的 BING_RESILIENCE_DEMO_TOKEN，仅用于本地演示登录。");
        var path = Path.Combine(Path.GetTempPath(), "bing-management-demo-" + Guid.NewGuid().ToString("N") + ".db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        var store = new RelationalOperationStore(_ => ValueTask.FromResult<DbConnection>(new SqliteConnection(connectionString)), ResilienceRelationalDialect.Sqlite);
        await store.InitializeAsync();
        using var protector = new AesGcmOperationDataProtector("demo", new Dictionary<string, byte[]> { ["demo"] = RandomNumberGenerator.GetBytes(32) });
        try
        {
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
            {
                o.Cookie.Name = "Bing.Resilience.Demo"; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.HttpOnly = true;
                o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
            });
            builder.Services.AddBingResilienceOperations(new ResilienceOperationDescriptor("demo.order", "v1", ResilienceOperationKind.IdempotentWrite, "demo"));
            builder.Services.AddSingleton<IResilienceOperationStore>(store); builder.Services.AddSingleton<IOperationDataProtector>(protector);
            builder.Services.AddBingDurableReplay(o => o.Authorize = (_, _) => ValueTask.FromResult(true),
                new ReplayHandlerRegistration("demo", "1", "demo.order", _ => new Handler()));
            builder.Services.AddBingSaga(o => o.Authorize = (_, _) => ValueTask.FromResult(true), new SagaHandlerRegistration("demo", "1", _ => new Handler()));
            builder.Services.AddBingResilienceOperationsManagement();
            await using var app = builder.Build(); app.UseAuthentication();
            app.MapGet("/login", () => Results.Content("""
                <!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>演示登录</title>
                <body><h1>本地容错管理演示</h1><form method="post" action="/login"><label>本地演示令牌 <input type="password" name="token" autocomplete="off" required></label><button>登录</button></form><p>登录后查询 Replay 操作 ID：demo-order。</p></body></html>
                """, "text/html", Encoding.UTF8));
            app.MapPost("/login", async (HttpContext context) =>
            {
                if (!context.Request.HasFormContentType || context.Request.ContentLength > 4096) return Results.BadRequest();
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(form["token"].ToString()), Encoding.UTF8.GetBytes(password))) return Results.Unauthorized();
                await context.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "local-demo-operator") }, CookieAuthenticationDefaults.AuthenticationScheme)));
                return Results.Redirect("/_resilience");
            });
            app.MapBingResilienceOperations(o =>
            {
                o.EnableConsole = true; o.ResolveAccessScope = _ => ValueTask.FromResult<string?>("demo");
                o.AuthorizeAsync = (context, _) => ValueTask.FromResult(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value == "local-demo-operator");
            });
            await app.Services.GetRequiredService<IReplayCoordinator>().EnqueueAsync(new("demo-order", "demo.order", "v1", "demo", "1", "demo", "demo-key", "{\"orderId\":1}", DateTimeOffset.UtcNow.AddHours(1)));
            await app.StartAsync();
            Console.WriteLine("演示登录：" + app.Urls.Single() + "/login（Ctrl+C 结束并清理专属数据库）");
            await app.WaitForShutdownAsync();
        }
        finally { foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix); }
    }
    private sealed class Handler : IReplayCommandHandler, ISagaStepHandler
    {
        public ValueTask<ReplayHandlerResult> ExecuteAsync(ReplayExecutionContext context, CancellationToken token) => ValueTask.FromResult(new ReplayHandlerResult(ReplayOutcome.Succeeded));
        public ValueTask<ReplayHandlerResult> ConfirmAsync(ReplayExecutionContext context, CancellationToken token) => ExecuteAsync(context, token);
        public ValueTask<SagaStepResult> ForwardAsync(SagaStepContext context, CancellationToken token) => ValueTask.FromResult(new SagaStepResult(SagaOutcome.Succeeded));
        public ValueTask<SagaStepResult> ConfirmAsync(SagaStepContext context, CancellationToken token) => ForwardAsync(context, token);
        public ValueTask<SagaStepResult> CompensateAsync(SagaStepContext context, CancellationToken token) => ForwardAsync(context, token);
    }
}
