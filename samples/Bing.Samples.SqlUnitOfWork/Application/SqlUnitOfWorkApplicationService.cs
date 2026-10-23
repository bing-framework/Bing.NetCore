using System.Data.Common;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Events.Cap;
using Bing.Events;
using Bing.Events.Messages;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Events;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Scenarios;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Testing;
using Bing.Samples.SqlUnitOfWork.Repositories;
using Bing.Uow;
using MySqlConnector;

namespace Bing.Samples.SqlUnitOfWork.Application;

/// <summary>
/// 组合 orders、reporting 和 EF 写入，并在应用层持有业务提交边界。
/// </summary>
public sealed class SqlUnitOfWorkApplicationService
{
    private readonly ISqlQuery _applicationQuery;
    private readonly ISqlExecutor _applicationExecutor;
    private readonly ISqlQueryFactory _queryFactory;
    private readonly IUnitOfWorkScopeManager _scopeManager;
    private readonly OrdersRepository _orders;
    private readonly ReportingRepository _reporting;
    private readonly SqlUnitOfWorkSampleOptions _options;
    private readonly SampleScopeIsolationProbe? _scopeProbe;
    private readonly SampleScopeLifetime? _scopeLifetime;
    private readonly IUnitOfWorkMessagePublisher? _messagePublisher;
    private readonly ISqlUnitOfWorkResourceAccessor? _resourceAccessor;
    private readonly ISimpleEventBus? _memoryEventBus;
    private readonly ScenarioOperationStore? _operationStore;

    public SqlUnitOfWorkApplicationService(ISqlQuery applicationQuery, ISqlExecutor applicationExecutor,
        ISqlQueryFactory queryFactory, IUnitOfWorkScopeManager scopeManager, OrdersRepository orders,
        ReportingRepository reporting, SqlUnitOfWorkSampleOptions options,
        IUnitOfWorkMessagePublisher? messagePublisher = null,
        ISqlUnitOfWorkResourceAccessor? resourceAccessor = null,
        ISimpleEventBus? memoryEventBus = null, ScenarioOperationStore? operationStore = null)
        : this(applicationQuery, applicationExecutor, queryFactory, scopeManager, orders, reporting, options,
            null, null, messagePublisher, resourceAccessor, memoryEventBus, operationStore)
    {
    }

    internal SqlUnitOfWorkApplicationService(ISqlQuery applicationQuery, ISqlExecutor applicationExecutor,
        ISqlQueryFactory queryFactory, IUnitOfWorkScopeManager scopeManager, OrdersRepository orders,
        ReportingRepository reporting, SqlUnitOfWorkSampleOptions options, SampleScopeIsolationProbe? scopeProbe,
        SampleScopeLifetime? scopeLifetime, IUnitOfWorkMessagePublisher? messagePublisher = null,
        ISqlUnitOfWorkResourceAccessor? resourceAccessor = null, ISimpleEventBus? memoryEventBus = null,
        ScenarioOperationStore? operationStore = null)
    {
        _applicationQuery = applicationQuery;
        _applicationExecutor = applicationExecutor;
        _queryFactory = queryFactory;
        _scopeManager = scopeManager;
        _orders = orders;
        _reporting = reporting;
        _options = options;
        _scopeProbe = scopeProbe;
        _scopeLifetime = scopeLifetime;
        _messagePublisher = messagePublisher;
        _resourceAccessor = resourceAccessor;
        _memoryEventBus = memoryEventBus;
        _operationStore = operationStore;
    }

    public async Task<ScenarioExecutionResult> ExecuteAsync(string scenario, string operationId,
        CancellationToken cancellationToken, string? targetOperationId = null)
    {
        _operationStore?.EnsureTracked(operationId, scenario);
        var readOnly = scenario == SqlUnitOfWorkScenarios.ScopeIsolationProbe;
        if (!_options.EfBridgeEnabled && scenario is SqlUnitOfWorkScenarios.EfDapperCommit or
            SqlUnitOfWorkScenarios.EfDapperRollback or SqlUnitOfWorkScenarios.EfCompleteWithoutSave)
        {
            throw new InvalidOperationException("EF Shared 场景需要以 SqlUow:Storage=EfBridge 启动。");
        }
        if (IsCapScenario(scenario) && !_options.CapMySqlMode)
            throw new InvalidOperationException("CAP 场景需要以 SqlUow:Storage=CapMySql 或 CapMySqlEfBridge 启动。");
        if (scenario == SqlUnitOfWorkScenarios.CapEfFirstCommit && !_options.EfBridgeEnabled)
            throw new InvalidOperationException("EF-first CAP 场景需要以 SqlUow:Storage=CapMySqlEfBridge 启动。");

        using var observation = _scopeProbe?.Begin(operationId, scenario);
        _scopeLifetime?.Attach(_scopeProbe, operationId);
        await using var scope = _scopeManager.Begin(
            readOnly ? new UnitOfWorkScopeOptions { IsTransactional = false } : null);
        observation?.Attach(scope, _scopeManager, _applicationQuery, _orders.Query, _orders.Context,
            _options.EfBridgeEnabled);

        var commit = true;
        var terminateReportingConnection = false;
        var factoryQueries = 0;
        var efSaveChangesAffected = 0;
        string? detail = null;

        switch (scenario)
        {
            case SqlUnitOfWorkScenarios.MemoryEventImmediate:
                await (_memoryEventBus ?? throw new InvalidOperationException(
                    "当前宿主未注册 scoped 内存事件总线.")).PublishAsync(
                    new SqlUnitOfWorkMemoryEvent(operationId));
                detail = "内存事件 handler 在发布调用中、当前依赖注入 scope 内完成。";
                break;

            case SqlUnitOfWorkScenarios.MemoryEventFailure:
                await (_memoryEventBus ?? throw new InvalidOperationException(
                    "当前宿主未注册 scoped 内存事件总线.")).PublishAsync(
                    new SqlUnitOfWorkMemoryEvent(operationId, fail: true));
                break;

            case SqlUnitOfWorkScenarios.MemoryEventAfterComplete:
            case SqlUnitOfWorkScenarios.MemoryEventAfterCompleteRollback:
                var afterCompleteEvent = new SqlUnitOfWorkMemoryEvent(operationId,
                    requireCompletedScope: true);
                scope.OnCompleted(() => (_memoryEventBus ?? throw new InvalidOperationException(
                        "当前宿主未注册 scoped 内存事件总线."))
                    .PublishAsync(afterCompleteEvent));
                _operationStore?.RecordProcessStep(operationId, operationId, "local",
                    "completion-callback-registered");
                if (scenario == SqlUnitOfWorkScenarios.MemoryEventAfterCompleteRollback)
                    commit = false;
                detail = "内存事件在当前 scope 的 Uow 完成回调中处理。";
                break;

            case SqlUnitOfWorkScenarios.CrossLayerCommit:
            case SqlUnitOfWorkScenarios.CrossLayerRollback:
                await InsertApplicationOrderAsync(operationId, "application", 1, "application", cancellationToken);
                await _orders.InsertAsync($"{operationId}:repository", operationId, "repository", 2,
                    "repository", cancellationToken);
                await RunLayerReadQueriesAsync(operationId, observation, cancellationToken);
                if (scenario == SqlUnitOfWorkScenarios.CrossLayerRollback)
                    commit = false;
                break;

            case SqlUnitOfWorkScenarios.MultiDatabaseCommit:
            case SqlUnitOfWorkScenarios.MultiDatabaseRollback:
                await InsertApplicationOrderAsync(operationId, "application", 1, "orders", cancellationToken);
                await _reporting.InsertAsync(operationId, "reporting", cancellationToken);
                if (scenario == SqlUnitOfWorkScenarios.MultiDatabaseRollback)
                    commit = false;
                break;

            case SqlUnitOfWorkScenarios.CapMultiSourceCommit:
            case SqlUnitOfWorkScenarios.CapMultiSourceRollback:
            case SqlUnitOfWorkScenarios.CapPartialCommit:
                await WriteCapOrdersAsync(operationId, cancellationToken,
                    scenario);
                await _reporting.InsertAsync(operationId, "reporting", cancellationToken);
                await PublishCapEventAsync("reporting", operationId, $"{operationId}:reporting-event",
                    cancellationToken, scenario: scenario);
                if (scenario == SqlUnitOfWorkScenarios.CapMultiSourceRollback)
                    commit = false;
                if (scenario == SqlUnitOfWorkScenarios.CapPartialCommit)
                    terminateReportingConnection = true;
                break;

            case SqlUnitOfWorkScenarios.CapConsumerRetry:
                await InsertApplicationOrderAsync(operationId, "cap-orders", 1, "consumer-retry",
                    cancellationToken);
                await PublishCapEventAsync("orders", operationId, $"{operationId}:retry-event",
                    cancellationToken, scenario: "retry-once");
                break;

            case SqlUnitOfWorkScenarios.CapMessageFirstCommit:
                await PublishCapEventAsync("orders", operationId, $"{operationId}:orders-message-first",
                    cancellationToken, scenario: scenario, publishBeforeBusinessEvent: true);
                await InsertApplicationOrderAsync(operationId, "cap-orders", 1, "message-first",
                    cancellationToken);
                await _reporting.InsertAsync(operationId, "reporting", cancellationToken);
                await PublishCapEventAsync("reporting", operationId, $"{operationId}:reporting-event",
                    cancellationToken, scenario: scenario);
                detail = "消息首先取得 orders 的当前 Uow 资源，随后业务写入复用该物理事务。";
                break;

            case SqlUnitOfWorkScenarios.CapEfFirstCommit:
                _orders.Context.OrderRecords.Add(CreateRecord(operationId, "cap-ef", 1, "ef-first"));
                efSaveChangesAffected = await _orders.Context.SaveChangesAsync(cancellationToken);
                await PublishCapEventAsync("orders", operationId, $"{operationId}:orders-ef-first",
                    cancellationToken, scenario: scenario);
                await _reporting.InsertAsync(operationId, "reporting", cancellationToken);
                await PublishCapEventAsync("reporting", operationId, $"{operationId}:reporting-event",
                    cancellationToken, scenario: scenario);
                detail = "EF SaveChanges 先建立 orders Uow 資源，CAP Outbox 復用同一 EF 事务。";
                break;

            case SqlUnitOfWorkScenarios.CapDuplicateEvent:
                await WriteCapOrdersAsync(operationId, cancellationToken, scenario);
                var duplicateEventId = $"{operationId}:duplicate-business-event";
                await PublishCapEventAsync("orders", operationId, duplicateEventId, cancellationToken,
                    scenario: "duplicate");
                await PublishCapEventAsync("orders", operationId, duplicateEventId, cancellationToken,
                    scenario: "duplicate", recordBusinessEvent: false);
                break;

            case SqlUnitOfWorkScenarios.CaughtSqlException:
                await RunCaughtSqlExceptionAsync(operationId, scope, cancellationToken);
                throw new InvalidOperationException("预期工作单元拒绝了已标记 rollback-only 的提交。");

            case SqlUnitOfWorkScenarios.FactoryQueryLifetime:
                for (var index = 0; index < 100; index++)
                {
                    using var query = _queryFactory.Create("orders");
                    var value = await query.Sql("SELECT @value", new { value = index })
                        .ScalarAsync<int>(cancellationToken: cancellationToken);
                    if (value != index)
                        throw new InvalidOperationException($"工厂查询 {index} 返回了非预期值 {value}。");
                    factoryQueries++;
                }

                await InsertApplicationOrderAsync(operationId, "factory-after", 1, "root-still-open",
                    cancellationToken);
                detail = "100 个工厂 Query 均已释放；随后注入的应用 Query 仍可使用当前 root。";
                break;

            case SqlUnitOfWorkScenarios.EfDapperCommit:
            case SqlUnitOfWorkScenarios.EfDapperRollback:
                _orders.Context.OrderRecords.Add(CreateRecord(operationId, "ef", 1, "saved-by-ef"));
                efSaveChangesAffected = await _orders.Context.SaveChangesAsync(cancellationToken);
                await _orders.InsertAsync($"{operationId}:dapper", operationId, "dapper", 2,
                    "written-by-dapper", cancellationToken);
                if (scenario == SqlUnitOfWorkScenarios.EfDapperRollback)
                    commit = false;
                break;

            case SqlUnitOfWorkScenarios.EfCompleteWithoutSave:
                _orders.Context.OrderRecords.Add(CreateRecord(operationId, "ef-unsaved", 1, "not-flushed"));
                await _orders.InsertAsync($"{operationId}:dapper", operationId, "dapper", 2,
                    "written-by-dapper", cancellationToken);
                detail = "Complete 只完成已登记资源；EF 跟踪实体因未调用 SaveChanges 而未写入。";
                break;

            case SqlUnitOfWorkScenarios.ScopeIsolationProbe:
                await RunReadOnlyScopeIsolationAsync(targetOperationId ?? operationId, observation,
                    cancellationToken);
                detail = "只读非事务 scope，用于验证不同宿主入口的业务 root 隔离。";
                break;

            default:
                throw new ArgumentException($"未知工作单元场景：{scenario}", nameof(scenario));
        }

        var queryOperationId = readOnly ? targetOperationId ?? operationId : operationId;
        var applicationCount = await CountAsync(_applicationQuery, queryOperationId, cancellationToken);
        var repositoryCount = await _orders.CountAsync(queryOperationId, cancellationToken);
        var reportingCount = await _reporting.CountAsync(readOnly ? operationId : queryOperationId, cancellationToken);
        var result = new ScenarioExecutionResult(
            operationId,
            scenario,
            scope.Id,
            _orders.UsesIndependentQuery(_applicationQuery),
            applicationCount,
            repositoryCount,
            reportingCount,
            factoryQueries,
            efSaveChangesAffected,
            detail,
            readOnly ? "ReadOnly" : commit ? "Committed" : "RolledBack");

        if (terminateReportingConnection)
            await TerminateReportingConnectionAsync(scope, cancellationToken);

        if (commit)
            await scope.CompleteAsync(cancellationToken);
        else
            await scope.RollbackAsync(cancellationToken);

        return result;
    }

    private async Task RunLayerReadQueriesAsync(string operationId,
        SampleScopeIsolationProbe.ScopeObservationLease? observation, CancellationToken cancellationToken)
    {
        var (applicationQuery, repositoryQuery) = CreateLayerReadQueries(operationId);
        var applicationState = CaptureQueryState(applicationQuery);
        var repositoryState = CaptureQueryState(repositoryQuery);

        RecordQueryState(observation, "application", "before-execution", applicationState);
        RecordQueryState(observation, "repository", "before-execution", repositoryState);
        RecordQueryState(observation, "application", "repeat-before-execution",
            CaptureQueryState(applicationQuery));
        RecordQueryState(observation, "repository", "repeat-before-execution",
            CaptureQueryState(repositoryQuery));

        var applicationRows = await applicationQuery.ToListAsync<OrderQueryRow>(cancellationToken);
        observation?.RecordQueryRows("application", "cross-layer", applicationRows);
        var repositoryRows = await repositoryQuery.ToListAsync<OrderQueryRow>(cancellationToken);
        observation?.RecordQueryRows("repository", "cross-layer", repositoryRows);

        RecordQueryState(observation, "application", "after-repository-execution",
            CaptureQueryState(applicationQuery, applicationState.Parameters));
        RecordQueryState(observation, "repository", "after-repository-execution",
            CaptureQueryState(repositoryQuery, repositoryState.Parameters));

        if (applicationRows.Count != 1 || applicationRows[0].OperationId != operationId ||
            applicationRows[0].Layer != "application" || applicationRows[0].Sequence != 1 ||
            applicationRows[0].Value != "application")
            throw new InvalidOperationException("应用层 Fluent Query 未返回自己的 application 行。");
        if (repositoryRows.Count != 1 || repositoryRows[0].OperationId != operationId ||
            repositoryRows[0].Layer != "repository" || repositoryRows[0].Sequence != 2 ||
            repositoryRows[0].Value != "repository")
            throw new InvalidOperationException("orders 仓储 Fluent Query 未返回自己的 repository 行。");
    }

    private async Task RunReadOnlyScopeIsolationAsync(string targetOperationId,
        SampleScopeIsolationProbe.ScopeObservationLease? observation, CancellationToken cancellationToken)
    {
        var (applicationQuery, repositoryQuery) = CreateLayerReadQueries(targetOperationId);
        await ExecuteReadOnlyLayerQueriesAsync(applicationQuery, repositoryQuery, observation,
            "before-gate", cancellationToken);
        if (observation != null)
            await observation.WaitAtReadOnlyGateAsync(cancellationToken);

        (applicationQuery, repositoryQuery) = CreateLayerReadQueries(targetOperationId);
        await ExecuteReadOnlyLayerQueriesAsync(applicationQuery, repositoryQuery, observation,
            "after-release", cancellationToken);
    }

    private static async Task ExecuteReadOnlyLayerQueriesAsync(SqlFluentQuery applicationQuery,
        SqlFluentQuery repositoryQuery, SampleScopeIsolationProbe.ScopeObservationLease? observation,
        string stage, CancellationToken cancellationToken)
    {
        var applicationState = CaptureQueryState(applicationQuery);
        var repositoryState = CaptureQueryState(repositoryQuery);
        RecordQueryState(observation, "application", $"{stage}-before", applicationState);
        RecordQueryState(observation, "repository", $"{stage}-before", repositoryState);
        RecordQueryState(observation, "application", $"{stage}-repeat",
            CaptureQueryState(applicationQuery));
        RecordQueryState(observation, "repository", $"{stage}-repeat",
            CaptureQueryState(repositoryQuery));

        var applicationRows = await applicationQuery.ToListAsync<OrderQueryRow>(cancellationToken);
        observation?.RecordQueryRows("application", stage, applicationRows);
        var repositoryRows = await repositoryQuery.ToListAsync<OrderQueryRow>(cancellationToken);
        observation?.RecordQueryRows("repository", stage, repositoryRows);

        RecordQueryState(observation, "application", $"{stage}-after-both",
            CaptureQueryState(applicationQuery, applicationState.Parameters));
        RecordQueryState(observation, "repository", $"{stage}-after-both",
            CaptureQueryState(repositoryQuery, repositoryState.Parameters));
    }

    private (SqlFluentQuery Application, SqlFluentQuery Repository) CreateLayerReadQueries(string operationId)
    {
        var applicationQuery = _applicationQuery.Query()
            .Select("Id,OperationId,Layer,Sequence,Value")
            .From("OrderRecords")
            .Where("OperationId", operationId);
        var repositoryQuery = _orders.CreateOperationQuery(operationId);

        applicationQuery.Where("Layer", "application");
        _orders.AddRepositoryFilters(repositoryQuery, operationId);
        applicationQuery.Where("Sequence", 1);
        applicationQuery.Where("Value", "application");
        return (applicationQuery, repositoryQuery);
    }

    private static (string Sql, IReadOnlyDictionary<string, object> Parameters) CaptureQueryState(
        SqlFluentQuery query, IReadOnlyDictionary<string, object>? parameters = null) =>
        (query.ToSql(), parameters ?? query.GetParams() ?? new Dictionary<string, object>());

    private static void RecordQueryState(SampleScopeIsolationProbe.ScopeObservationLease? observation,
        string role, string stage, (string Sql, IReadOnlyDictionary<string, object> Parameters) state) =>
        observation?.RecordQueryState(role, stage, state.Sql, state.Parameters);

    private Task<int> InsertApplicationOrderAsync(string operationId, string suffix, int sequence, string value,
        CancellationToken cancellationToken)
    {
        _operationStore?.RecordProcessStep(operationId, $"{operationId}:{suffix}", "orders",
            "order-row-sql-started");
        return _applicationExecutor.ExecuteSqlAsync(
            "INSERT INTO OrderRecords (Id, OperationId, Layer, Sequence, Value) " +
            "VALUES (@id, @operationId, @layer, @sequence, @value)",
            new
            {
                id = $"{operationId}:{suffix}",
                operationId,
                layer = "application",
                sequence,
                value
            }, cancellationToken: cancellationToken);
    }

    private static bool IsCapScenario(string scenario) => scenario is
        SqlUnitOfWorkScenarios.CapMultiSourceCommit or
        SqlUnitOfWorkScenarios.CapMultiSourceRollback or
        SqlUnitOfWorkScenarios.CapMessageFirstCommit or
        SqlUnitOfWorkScenarios.CapEfFirstCommit or
        SqlUnitOfWorkScenarios.CapDuplicateEvent or
        SqlUnitOfWorkScenarios.CapConsumerRetry or
        SqlUnitOfWorkScenarios.CapPartialCommit;

    private async Task WriteCapOrdersAsync(string operationId, CancellationToken cancellationToken,
        string scenario = "committed")
    {
        await InsertApplicationOrderAsync(operationId, "cap-orders", 1, "orders", cancellationToken);
        await PublishCapEventAsync("orders", operationId, $"{operationId}:orders-event", cancellationToken,
            scenario: scenario);
    }

    private async Task PublishCapEventAsync(string source, string operationId, string eventId,
        CancellationToken cancellationToken, string scenario = "committed", bool recordBusinessEvent = true,
        bool publishBeforeBusinessEvent = false)
    {
        var publisher = _messagePublisher ??
                        throw new InvalidOperationException("当前宿主未注册 CAP Uow 消息发布入口。");
        var rootId = _scopeManager.Current?.Id.ToString("N") ??
                     throw new InvalidOperationException("CAP 场景要求活动的业务 Uow root。");
        var traceId = Bing.Tracing.TraceIdContext.Current?.TraceId ?? operationId;
        var payload = new SqlUnitOfWorkEventPayload(operationId, eventId, traceId, rootId, source, scenario);
        var message = new MessageEvent<SqlUnitOfWorkEventPayload>(payload)
        {
            Id = eventId,
            Name = source == "orders" ? SqlUnitOfWorkEventTopics.Orders : SqlUnitOfWorkEventTopics.Reporting,
            Callback = string.Empty,
            Send = false
        };
        var topic = message.Name;
        _operationStore?.RecordCapEvent(operationId, scenario, eventId, traceId, rootId, source, topic);
        if (recordBusinessEvent && !publishBeforeBusinessEvent)
        {
            if (source == "orders")
                await _orders.RegisterCapBusinessEventAsync(payload, topic, cancellationToken);
            else
                await _reporting.RegisterCapBusinessEventAsync(payload, topic, cancellationToken);
        }
        _operationStore?.MarkPublisherCallStarted(operationId, eventId, source);
        await publisher.PublishAsync(source, message, cancellationToken);
        _operationStore?.MarkPublisherReturned(operationId, eventId, source);
        if (recordBusinessEvent && publishBeforeBusinessEvent)
        {
            if (source == "orders")
                await _orders.RegisterCapBusinessEventAsync(payload, topic, cancellationToken);
            else
                await _reporting.RegisterCapBusinessEventAsync(payload, topic, cancellationToken);
        }
    }

    private async Task TerminateReportingConnectionAsync(IUnitOfWorkScope scope,
        CancellationToken cancellationToken)
    {
        if (_resourceAccessor == null || string.IsNullOrWhiteSpace(_options.MySqlAdminConnectionString))
            throw new InvalidOperationException("部分提交场景需要资源访问器和本地 MySQL 管理连接配置。");

        var resource = _resourceAccessor.GetResource(scope,
            new DatabaseContext { DbKey = "reporting" }, createDapperResource: true,
            requireComparableIdentity: true) ??
            throw new InvalidOperationException("未能取得 reporting 数据源的 Uow 资源。");
        await resource.EnsureReadyAsync(cancellationToken);
        if (resource.Connection is not DbConnection connection || resource.Transaction is not DbTransaction transaction)
            throw new InvalidOperationException("reporting 资源未提供 CAP MySQL 所需的 ADO.NET 事务连接。");
        await using var idCommand = connection.CreateCommand();
        idCommand.Transaction = transaction;
        idCommand.CommandText = "SELECT CONNECTION_ID()";
        var connectionId = Convert.ToInt64(await idCommand.ExecuteScalarAsync(cancellationToken));

        await using var adminConnection = new MySqlConnection(_options.MySqlAdminConnectionString);
        await adminConnection.OpenAsync(cancellationToken);
        await using var killCommand = adminConnection.CreateCommand();
        killCommand.CommandText = $"KILL CONNECTION {connectionId}";
        await killCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Task<int> CountAsync(ISqlQuery query, string operationId, CancellationToken cancellationToken) =>
        query.Sql("SELECT COUNT(*) FROM OrderRecords WHERE OperationId = @operationId", new { operationId })
            .ScalarAsync<int>(cancellationToken: cancellationToken);

    private async Task RunCaughtSqlExceptionAsync(string operationId, IUnitOfWorkScope scope,
        CancellationToken cancellationToken)
    {
        var duplicateId = $"{operationId}:duplicate";
        await _orders.InsertAsync(duplicateId, operationId, "before-error", 1, "must-roll-back",
            cancellationToken);

        try
        {
            await _orders.InsertAsync(duplicateId, operationId, "duplicate", 2, "constraint-failure",
                cancellationToken);
        }
        catch (DbException sqlException)
        {
            if (!scope.IsRollbackOnly)
                throw new InvalidOperationException("捕获 SQL 异常后，root 未标记为 rollback-only。", sqlException);

            try
            {
                await scope.CompleteAsync(cancellationToken);
            }
            catch (InvalidOperationException completionException)
            {
                throw new InvalidOperationException(
                    $"已捕获 {sqlException.GetType().Name}；scope rollback-only={scope.IsRollbackOnly}，提交已拒绝。",
                    completionException);
            }

            throw new InvalidOperationException("rollback-only 工作单元意外允许完成。");
        }
    }

    private static OrderRecord CreateRecord(string operationId, string suffix, int sequence, string value) => new()
    {
        Id = $"{operationId}:{suffix}",
        OperationId = operationId,
        Layer = suffix,
        Sequence = sequence,
        Value = value
    };
}
