using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Service.FileStorage;
using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bing.Admin.Apis.Commons;

/// <summary>显式启用且受宿主管理权限保护的文件健康与 Prometheus 入口。</summary>
[ApiController]
[Authorize]
[ServiceFilter(typeof(FileManagementExceptionFilter))]
public sealed class FileOperationsController : ControllerBase
{
    private readonly AdminFileOperationsOptions _options;
    private readonly FileManagementService _files;
    private readonly IFileManagementStore _store;
    private readonly FileManagementHealthCheck _health;
    private readonly AdminFileMetricsExporter _metrics;
    /// <summary>接收启用状态及文件模块依赖；禁用时不解析业务能力。</summary>
    public FileOperationsController(AdminFileOperationsOptions options = null, FileManagementService files = null,
        IFileManagementStore store = null, FileManagementHealthCheck health = null, AdminFileMetricsExporter metrics = null)
    { _options = options; _files = files; _store = store; _health = health; _metrics = metrics; }

    /// <summary>检查文件元数据库、存储探针及 worker 就绪状态。</summary>
    [HttpGet("/internal/health/files")]
    public async Task<IActionResult> HealthAsync(CancellationToken ct)
    {
        if (_options?.HealthEnabled != true) return NotFound();
        await AuthorizeAsync(ct);
        var result = await _health.CheckHealthAsync(new HealthCheckContext(), ct);
        return new JsonResult(new { status = result.Status.ToString(), description = result.Description })
            { StatusCode = result.Status == HealthStatus.Healthy ? 200 : 503 };
    }

    /// <summary>导出全局队列快照和本进程传输指标。</summary>
    [HttpGet("/internal/metrics/files")]
    public async Task<IActionResult> MetricsAsync(CancellationToken ct)
    {
        if (_options?.MetricsEnabled != true) return NotFound();
        await AuthorizeAsync(ct);
        return Content(_metrics.Export(await _store.GetOperationalSnapshotAsync(ct)), "text/plain; version=0.0.4", Encoding.UTF8);
    }

    private async Task AuthorizeAsync(CancellationToken ct)
    {
        var context = await _files.GetContextAsync(manage: true, ct);
        if (context.ScopeKind != StorageScopeKind.Host) throw new FileManagementException("forbidden", "运维接口仅支持宿主范围。");
    }
}

/// <summary>Admin 内部文件运维入口的显式开关。</summary>
public sealed class AdminFileOperationsOptions
{
    /// <summary>启用受保护的健康接口。</summary>
    public bool HealthEnabled { get; set; }
    /// <summary>启用指标监听及受保护的抓取接口。</summary>
    public bool MetricsEnabled { get; set; }
}
