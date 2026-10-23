namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Events;

/// <summary>
/// 跨进程 CAP 示例消息，用于把业务操作、事件和消费结果关联起来。
/// </summary>
public sealed record SqlUnitOfWorkEventPayload(
    string OperationId,
    string EventId,
    string TraceId,
    string RootId,
    string Source,
    string Scenario);

/// <summary>
/// 定义两个数据源分别使用的 CAP 主题。
/// </summary>
public static class SqlUnitOfWorkEventTopics
{
    public const string Orders = "bing.sqluow.orders.changed";
    public const string Reporting = "bing.sqluow.reporting.changed";
}
