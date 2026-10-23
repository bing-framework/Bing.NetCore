using Bing.Data.Sql;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Events;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;

namespace Bing.Samples.SqlUnitOfWork.Repositories;

/// <summary>
/// reporting 仓储：每次操作都通过工厂明确选择 reporting 数据源。
/// </summary>
public sealed class ReportingRepository
{
    private readonly ISqlQueryFactory _queryFactory;
    private readonly ISqlExecutorFactory _executorFactory;
    private readonly ScenarioOperationStore? _operationStore;

    public ReportingRepository(ISqlQueryFactory queryFactory, ISqlExecutorFactory executorFactory,
        ScenarioOperationStore? operationStore = null)
    {
        _queryFactory = queryFactory;
        _executorFactory = executorFactory;
        _operationStore = operationStore;
    }

    public async Task<int> InsertAsync(string operationId, string value, CancellationToken cancellationToken)
    {
        _operationStore?.RecordProcessStep(operationId, $"{operationId}:reporting", "reporting",
            "report-row-sql-started");
        using var query = _queryFactory.Create("reporting");
        using var executor = _executorFactory.Create("reporting");

        await executor.ExecuteSqlAsync(
            "INSERT INTO ReportRecords (Id, OperationId, Value) VALUES (@id, @operationId, @value)",
            new { id = $"{operationId}:reporting", operationId, value }, cancellationToken: cancellationToken);

        return await query.Sql(
                "SELECT COUNT(*) FROM ReportRecords WHERE OperationId = @operationId",
                new { operationId })
            .ScalarAsync<int>(cancellationToken: cancellationToken);
    }

    public async Task<int> RegisterCapBusinessEventAsync(SqlUnitOfWorkEventPayload message, string topic,
        CancellationToken cancellationToken)
    {
        _operationStore?.RecordProcessStep(message.OperationId, message.EventId, message.Source,
            "business-event-sql-started");
        using var executor = _executorFactory.Create("reporting");
        return await executor.ExecuteSqlAsync(
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

    public async Task<int> CountAsync(string operationId, CancellationToken cancellationToken)
    {
        using var query = _queryFactory.Create("reporting");
        return await query.Sql(
                "SELECT COUNT(*) FROM ReportRecords WHERE OperationId = @operationId",
                new { operationId })
            .ScalarAsync<int>(cancellationToken: cancellationToken);
    }
}
