using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.Auditing;

/// <summary>
/// 将审计日志写入应用程序日志的默认审计存储实现。
/// </summary>
[Dependency(ServiceLifetime.Singleton, TryAdd = true)]
public class SimpleLogAuditingStore : IAuditingStore
{
    /// <summary>
    /// 初始化 <see cref="SimpleLogAuditingStore"/> 的实例，并使用空日志记录器。
    /// </summary>
    public SimpleLogAuditingStore()
    {
        Logger = NullLogger<SimpleLogAuditingStore>.Instance;
    }

    /// <summary>
    /// 获取或设置用于写入审计日志的日志记录器。
    /// </summary>
    public ILogger<SimpleLogAuditingStore> Logger { get; set; }

    /// <inheritdoc />
    /// <remarks>当前实现仅将审计日志文本写入信息级日志，不执行持久化存储。</remarks>
    public Task SaveAsync(AuditLogInfo auditInfo)
    {
        Logger.LogInformation(auditInfo.ToString());
        return Task.FromResult(0);
    }
}
