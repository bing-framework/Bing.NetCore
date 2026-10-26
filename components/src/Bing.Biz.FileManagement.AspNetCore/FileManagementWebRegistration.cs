using Bing.FileStorage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bing.Biz.FileManagement.AspNetCore;

public sealed class FileManagementExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.HttpContext.Response.HasStarted) return;
        var (status, code) = context.Exception switch
        {
            FileManagementException e => (e.Code switch { "unauthenticated" => 401, "forbidden" => 403, "not_found" => 404,
                "conflict" or "bound" or "new_upload_required" => 409, "capacity" => 429, "configuration" => 503, _ => 400 }, e.Code),
            FileStorageException e => (e.Error switch { FileStorageError.NotFound or FileStorageError.UploadNotFound => 404,
                FileStorageError.AccessDenied => 403, FileStorageError.AlreadyExists or FileStorageError.Conflict => 409,
                FileStorageError.CapacityExceeded => 507, FileStorageError.Unavailable or FileStorageError.OutcomeUnknown or FileStorageError.Configuration => 503, _ => 400 }, e.Error.ToString()),
            ArgumentException => (400, "invalid"),
            _ => (0, "")
        };
        if (status == 0) return;
        context.Result = new ObjectResult(new ProblemDetails { Status = status, Title = "文件请求未完成", Extensions = { ["code"] = code } }) { StatusCode = status };
        context.ExceptionHandled = true;
        if (status == 429) context.HttpContext.Response.Headers["Retry-After"] = "5";
    }
}
public static class FileManagementWebRegistration
{
    public static IServiceCollection AddBingFileManagementWeb(this IServiceCollection services, bool runBackgroundWorker = true,
        Action<FileManagementHealthOptions>? configureHealth = null, Action<FileManagementConsoleOptions>? configureConsole = null)
    {
        services.AddDataProtection();
        services.AddScoped<FileManagementExceptionFilter>();
        var console = new FileManagementConsoleOptions();
        configureConsole?.Invoke(console);
        services.AddSingleton(console);
        services.AddControllers().AddApplicationPart(typeof(FileManagementConsoleController).Assembly);
        var health = new FileManagementHealthOptions { ExpectWorker = runBackgroundWorker };
        configureHealth?.Invoke(health);
        if (health.Timeout <= TimeSpan.Zero || health.Timeout > TimeSpan.FromSeconds(30) || health.WorkerStaleAfter < TimeSpan.FromSeconds(10) ||
            health.Probes.Count > 32 || health.Probes.Any(p => p.HostScope == !string.IsNullOrEmpty(p.TenantId)))
            throw new ArgumentException("文件健康检查配置无效。");
        services.AddSingleton(health);
        services.AddScoped<FileManagementHealthCheck>();
        services.AddHealthChecks().AddCheck<FileManagementHealthCheck>("bing-files", tags: new[] { "ready", "files" });
        if (runBackgroundWorker) services.AddHostedService<FileManagementWorker>();
        return services;
    }
}
internal sealed class FileManagementWorker : BackgroundService
{
    private readonly FileOperationScheduler _scheduler;
    public FileManagementWorker(FileOperationScheduler scheduler) { _scheduler = scheduler; }
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => _scheduler.RunAsync(stoppingToken);
}
