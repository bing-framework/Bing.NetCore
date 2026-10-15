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
    /// 初始化一个 <see cref="SimpleLogAuditingStore" /> 类型的实例。
    /// </summary>
    /// <remarks>此构造函数使用空日志记录器。</remarks>
    public SimpleLogAuditingStore()
    {
        Logger = NullLogger<SimpleLogAuditingStore>.Instance;
    }

    /// <summary>
    /// 初始化一个 <see cref="SimpleLogAuditingStore" /> 类型的实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public SimpleLogAuditingStore(ILogger<SimpleLogAuditingStore> logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取或设置用于写入审计日志的日志记录器。
    /// </summary>
    public ILogger<SimpleLogAuditingStore> Logger { get; set; }

    /// <inheritdoc />
    /// <remarks>仅输出有限元数据；完整参数和异常由调用方提供的审计存储处理。</remarks>
    public Task SaveAsync(AuditLogInfo auditInfo)
    {
        if (auditInfo == null)
            throw new ArgumentNullException(nameof(auditInfo));
        Logger.LogInformation("审计事件：状态码 {StatusCode}，关联标识 {CorrelationId}",
            auditInfo.HttpStatusCode, auditInfo.CorrelationId);
        return Task.FromResult(0);
    }
}
