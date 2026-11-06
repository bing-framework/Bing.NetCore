using Bing.FileStorage;
using Bing.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Biz.FileManagement;

public sealed partial class FileContainerPolicy
{
    public FileVisibility Visibility { get; set; } = FileVisibility.Private;
    public long MaxFileLength { get; set; } = 100L * 1024 * 1024 * 1024;
    /// <summary>个人附件发布副本使用的公共目标；不参与个人偏好选择。</summary>
    public string? BusinessStorageTargetId { get; set; }
}
public sealed partial class FileManagementOptions
{
    public Dictionary<string, FileContainerPolicy> Containers { get; } = new(StringComparer.Ordinal)
    { ["attachments"] = new() };
    public int PartSize { get; set; } = 16 * 1024 * 1024;
    public TimeSpan PartSignatureLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan DownloadSignatureLifetime { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan SessionMaxLifetime { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan ReplicaRetention { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan OperationLease { get; set; } = TimeSpan.FromMinutes(2);
    public int MaxOperationAttempts { get; set; } = 5;
    /// <summary>每个宿主的初始化/删除任务执行槽数量，范围 1～16。</summary>
    public int ShortOperationConcurrency { get; set; } = 2;
    /// <summary>每个宿主的完成/迁移执行槽数量，范围 1～16；默认一个，限制大文件同时占用磁盘。</summary>
    public int LongOperationConcurrency { get; set; } = 1;
    /// <summary>空闲、抢占冲突或调度异常后的轮询间隔，范围 100 毫秒～1 分钟。</summary>
    public TimeSpan OperationPollInterval { get; set; } = TimeSpan.FromSeconds(1);

    internal void ValidateScheduling()
    {
        if (ShortOperationConcurrency is < 1 or > 16 || LongOperationConcurrency is < 1 or > 16 ||
            OperationPollInterval < TimeSpan.FromMilliseconds(100) || OperationPollInterval > TimeSpan.FromMinutes(1))
            throw new ArgumentException("文件任务调度配置越界。");
    }
}
public enum FileBusinessAccess { Read, Bind, Unbind }
public interface IFileAccessAuthorizer
{
    /// <summary>检查当前用户对目标业务对象的权限；上层已检查文件范围。未知业务类型必须拒绝。</summary>
    Task<bool> AuthorizeAsync(string businessType, string businessId, FileBusinessAccess access, CancellationToken ct = default);
}
public interface IFilePermissionChecker
{
    /// <summary>权限名为 files.upload / files.publish / files.manage / files.host；不能硬编码角色名。</summary>
    Task<bool> IsGrantedAsync(string permission, CancellationToken ct = default);
}
internal sealed class DenyBusinessAccess : IFileAccessAuthorizer
{
    public Task<bool> AuthorizeAsync(string businessType, string businessId, FileBusinessAccess access, CancellationToken ct = default) => Task.FromResult(false);
}
public sealed class ClaimFilePermissionChecker : IFilePermissionChecker
{
    private readonly ICurrentUser _user;
    public ClaimFilePermissionChecker(ICurrentUser user) { _user = user; }
    public Task<bool> IsGrantedAsync(string permission, CancellationToken ct = default)
        => Task.FromResult(_user.IsAuthenticated && _user.FindClaims("permission").Any(c => c.Value == permission));
}
public static class FileManagementRegistration
{
    public static IServiceCollection AddBingFileManagement(this IServiceCollection services, Action<FileManagementOptions>? configure = null)
    {
        var options = new FileManagementOptions(); configure?.Invoke(options);
        options.ValidateScheduling();
        options.ValidateEnhancements();
        if (options.PartSize < 5 * 1024 * 1024 || options.PartSize > 128 * 1024 * 1024 ||
            options.SessionIdleTimeout <= TimeSpan.Zero || options.SessionMaxLifetime < options.SessionIdleTimeout ||
            options.CleanupInterval < TimeSpan.FromSeconds(1) || options.OperationLease < TimeSpan.FromSeconds(10) ||
            options.MaxOperationAttempts < 1 || options.ReplicaRetention < TimeSpan.Zero ||
            options.PartSignatureLifetime <= TimeSpan.Zero || options.PartSignatureLifetime > TimeSpan.FromMinutes(15) ||
            options.DownloadSignatureLifetime <= TimeSpan.Zero || options.DownloadSignatureLifetime > TimeSpan.FromMinutes(15))
            throw new ArgumentException("文件管理配置越界。");
        foreach (var pair in options.Containers)
        {
            FileStorageNames.ValidateIdentifier(pair.Key, nameof(options.Containers));
            if (pair.Value.MaxFileLength < 0 || pair.Value.MaxFileLength > Math.Min(100L * 1024 * 1024 * 1024, (long)options.PartSize * 10000))
                throw new ArgumentException("容器文件上限超过允许范围。");
        }
        services.AddSingleton(options);
        services.TryAddScoped<IFilePermissionChecker, ClaimFilePermissionChecker>();
        services.TryAddScoped<IFileAccessAuthorizer, DenyBusinessAccess>();
        services.AddScoped<FileManagementService>();
        services.TryAddScoped<IFileUploadStoreSelector, FileUploadStoreSelector>();
        services.AddScoped<FileStorageConnectionService>();
        services.AddScoped<FileBindingCopyService>();
        services.AddScoped<FileOperationProcessor>();
        services.AddScoped<FileMigrationService>();
        services.AddScoped<FileMigrationBatchService>();
        services.AddScoped<FileOperationAdministration>();
        services.AddScoped<FileResourceGate>();
        services.AddScoped<FileOutboxDispatcher>();
        services.AddScoped<FileGovernanceAdministration>();
        services.AddScoped<FileContentInspector>();
        services.TryAddSingleton<FileOperationScheduler>();
        services.TryAddSingleton<FileOperationalMetrics>();
        return services;
    }
}
