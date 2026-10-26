using Bing.FileStorage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bing.Biz.FileManagement.AspNetCore;

public sealed class FileStorageHealthProbe
{
    public string StoreName { get; set; } = "";
    public string? TenantId { get; set; }
    public bool HostScope { get; set; }
    public string Container { get; set; } = "attachments";
    public string ObjectKey { get; set; } = "health-probe";
    /// <summary>可以指向预先部署的对象；未要求存在时仅验证存储可达且查询成功。</summary>
    public bool RequireObject { get; set; }
}
public sealed class FileManagementHealthOptions
{
    public bool ExpectWorker { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan WorkerStaleAfter { get; set; } = TimeSpan.FromMinutes(5);
    public List<FileStorageHealthProbe> Probes { get; set; } = new();
}
/// <summary>只读、可取消的元数据/存储探针；失败结果不包含 SDK 异常或地址凭据。</summary>
public sealed class FileManagementHealthCheck : IHealthCheck
{
    private readonly IFileManagementStore _store;
    private readonly IFileStorageFactory _storage;
    private readonly FileOperationScheduler _scheduler;
    private readonly FileManagementHealthOptions _options;
    public FileManagementHealthCheck(IFileManagementStore store, IFileStorageFactory storage, FileOperationScheduler scheduler, FileManagementHealthOptions options)
    { _store = store; _storage = storage; _scheduler = scheduler; _options = options; }
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        try
        {
            var snapshot = await _store.GetOperationalSnapshotAsync(timeout.Token);
            var data = new Dictionary<string, object> { ["statistics"] = snapshot };
            foreach (var probe in _options.Probes)
            {
                var scope = probe.HostScope ? StorageScope.Host : StorageScope.ForTenant(probe.TenantId!);
                var result = await _storage.GetStorage(probe.StoreName, scope).GetPropertiesAsync(new(probe.Container, probe.ObjectKey), timeout.Token);
                if (probe.RequireObject && result == null) return HealthCheckResult.Unhealthy("文件存储探针对象缺失。", data: data);
            }
            if (_options.ExpectWorker && (!_scheduler.IsRunning || DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _scheduler.LastMaintenanceAt > _options.WorkerStaleAfter.TotalMilliseconds))
                return HealthCheckResult.Degraded("文件后台维护尚未就绪或长时间未成功。", data: data);
            return HealthCheckResult.Healthy(_options.Probes.Count == 0 ? "元数据库正常；未配置存储探针。" : "元数据库和配置的存储探针正常。", data);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return HealthCheckResult.Unhealthy("文件依赖不可用或探针超时。"); }
    }
}
