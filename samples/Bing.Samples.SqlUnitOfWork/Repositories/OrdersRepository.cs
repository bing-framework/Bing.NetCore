using Bing.Data.Sql;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Application;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Events;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;

namespace Bing.Samples.SqlUnitOfWork.Repositories;

/// <summary>
/// orders 仓储：自己的 Query 实例用于读取，Executor 负责写入。
/// </summary>
public sealed class OrdersRepository
{
    private readonly ISqlQuery _query;
    private readonly ISqlExecutor _executor;
    private readonly ScenarioOperationStore? _operationStore;

    public OrdersRepository(ISqlQuery query, ISqlExecutor executor,
        SampleUnitOfWorkDbContext context, ScenarioOperationStore? operationStore = null)
    {
        _query = query;
        _executor = executor;
        Context = context;
        _operationStore = operationStore;
    }

    internal SampleUnitOfWorkDbContext Context { get; }
    internal ISqlQuery Query => _query;

    internal bool UsesIndependentQuery(ISqlQuery applicationQuery) =>
        !ReferenceEquals(_query, applicationQuery);

    public Task<int> InsertAsync(string id, string operationId, string layer, int sequence, string value,
        CancellationToken cancellationToken)
    {
        _operationStore?.RecordProcessStep(operationId, id, "orders", "order-row-sql-started");
        return _executor.ExecuteSqlAsync(
            "INSERT INTO OrderRecords (Id, OperationId, Layer, Sequence, Value) " +
            "VALUES (@id, @operationId, @layer, @sequence, @value)",
            new { id, operationId, layer, sequence, value }, cancellationToken: cancellationToken);
    }

    public Task<int> CountAsync(string operationId, CancellationToken cancellationToken) =>
        _query.Sql(
                "SELECT COUNT(*) FROM OrderRecords WHERE OperationId = @operationId",
                new { operationId })
            .ScalarAsync<int>(cancellationToken: cancellationToken);

    public Task<int> RegisterCapBusinessEventAsync(SqlUnitOfWorkEventPayload message, string topic,
        CancellationToken cancellationToken)
    {
        _operationStore?.RecordProcessStep(message.OperationId, message.EventId, message.Source,
            "business-event-sql-started");
        return _executor.ExecuteSqlAsync(
            "INSERT IGNORE INTO CapBusinessEvents " +
            "(EventId, OperationId, TraceId, RootId, Source, Topic, Scenario, CreatedAt) " +
            "VALUES (@eventId, @operationId, @traceId, @rootId, @source, @topic, @scenario, UTC_TIMESTAMP(6))",
            new
            {
                eventId = message.EventId,
                message.OperationId,
                message.TraceId,
                message.RootId,
                message.Source,
                topic,
                message.Scenario
            }, cancellationToken: cancellationToken);
    }

    internal SqlFluentQuery CreateOperationQuery(string operationId) =>
        _query.Query()
            .Select("Id,OperationId,Layer,Sequence,Value")
            .From("OrderRecords")
            .Where("OperationId", operationId);

    internal void AddRepositoryFilters(SqlFluentQuery query, string operationId)
    {
        query.Where("Id", $"{operationId}:repository");
        query.Where("Sequence", 2);
        query.Where("Value", "repository");
    }
}

internal sealed class OrderQueryRow
{
    public string Id { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string Layer { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string Value { get; set; } = string.Empty;
}
