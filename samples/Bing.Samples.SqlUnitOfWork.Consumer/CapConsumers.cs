using Bing.Samples.SqlUnitOfWork.Infrastructure.Events;
using DotNetCore.CAP;

namespace Bing.Samples.SqlUnitOfWork.Consumer;

/// <summary>
/// 仅由 orders CAP runtime 注册的处理器。
/// </summary>
public sealed class OrdersCapConsumer : ICapSubscribe
{
    private readonly CapConsumerProcessingService _processor;

    public OrdersCapConsumer(CapConsumerProcessingService processor) => _processor = processor;

    [CapSubscribe(SqlUnitOfWorkEventTopics.Orders, Group = "sqluow-orders-consumer")]
    public Task HandleAsync(SqlUnitOfWorkEventPayload message, CancellationToken cancellationToken) =>
        _processor.ProcessAsync(message, cancellationToken);
}

/// <summary>
/// 仅由 reporting CAP runtime 注册的处理器。
/// </summary>
public sealed class ReportingCapConsumer : ICapSubscribe
{
    private readonly CapConsumerProcessingService _processor;

    public ReportingCapConsumer(CapConsumerProcessingService processor) => _processor = processor;

    [CapSubscribe(SqlUnitOfWorkEventTopics.Reporting, Group = "sqluow-reporting-consumer")]
    public Task HandleAsync(SqlUnitOfWorkEventPayload message, CancellationToken cancellationToken) =>
        _processor.ProcessAsync(message, cancellationToken);
}
