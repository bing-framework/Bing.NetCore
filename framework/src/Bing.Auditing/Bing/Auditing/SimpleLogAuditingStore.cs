using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.Auditing;

/// <summary>
/// 简单的日志审计存储器
/// </summary>
[Dependency(ServiceLifetime.Singleton, TryAdd = true)]
public class SimpleLogAuditingStore : IAuditingStore
{
    /// <summary>
    /// 初始化一个<see cref="SimpleLogAuditingStore"/>类型的实例
    /// </summary>
    public SimpleLogAuditingStore()
    {
        Logger = NullLogger<SimpleLogAuditingStore>.Instance;
    }

    /// <summary>
    /// 日志
    /// </summary>
    public ILogger<SimpleLogAuditingStore> Logger { get; set; }

    /// <summary>
    /// 保存
    /// </summary>
    /// <param name="auditInfo">审计日志信息</param>
    public Task SaveAsync(AuditLogInfo auditInfo)
    {
        Logger.LogInformation(auditInfo.ToString());
        return Task.FromResult(0);
    }
}
