using System.Runtime.CompilerServices;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Events.Cap;
using Bing.Events.Messages;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Events;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Scenarios;
using Bing.Samples.SqlUnitOfWork.Repositories;
using Bing.Tracing;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;

namespace Bing.Samples.SqlUnitOfWork.Application;

/// <summary>
/// 在 executor 建立的独立 scope 和 root 中运行一项示例业务操作。
/// </summary>
public sealed class OperationScenarioService
{
    private static readonly ConditionalWeakTable<object, ObjectIdentity> ObjectIdentities = new();
    private static long _nextObjectIdentity;

    private readonly OrdersRepository _orders;
    private readonly ReportingRepository _reporting;
    private readonly SampleUnitOfWorkDbContext _dbContext;
    private readonly SqlUnitOfWorkSampleOptions _options;
    private readonly IUnitOfWorkScopeManager _scopeManager;
    private readonly IDatabaseContextAccessor _databaseContextAccessor;
    private readonly ScenarioOperationStore _operations;
    private readonly IUnitOfWorkMessagePublisher? _messagePublisher;

    public OperationScenarioService(OrdersRepository orders, ReportingRepository reporting,
        SampleUnitOfWorkDbContext dbContext,
        SqlUnitOfWorkSampleOptions options, IUnitOfWorkScopeManager scopeManager,
        IDatabaseContextAccessor databaseContextAccessor,
        ScenarioOperationStore operations, IUnitOfWorkMessagePublisher? messagePublisher = null)
    {
        _orders = orders;
        _reporting = reporting;
        _dbContext = dbContext;
        _options = options;
        _scopeManager = scopeManager;
        _databaseContextAccessor = databaseContextAccessor;
        _operations = operations;
        _messagePublisher = messagePublisher;
    }

    public async Task<ScenarioExecutionResult> ExecuteAsync(string scenario, string operationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scope = _scopeManager.Current ??
            throw new InvalidOperationException("OperationScenarioService 必须由 BingOperationExecutor 执行。");
        var trace = TraceIdContext.Current;
        var identity = new ScenarioOperationIdentityEvidence(
            GetObjectIdentity(this),
            GetObjectIdentity(_scopeManager),
            scope.Id,
            GetObjectIdentity(_dbContext),
            GetObjectIdentity(_orders.Query),
            ReferenceEquals(scope, _scopeManager.Current),
            ReferenceEquals(_orders.Context, _dbContext),
            trace?.TraceId,
            trace?.RootId,
            trace?.ParentId,
            trace?.ChildId,
            _databaseContextAccessor.Current?.TenantId);
        _operations.RecordExecutionIdentity(operationId, identity);
        var queryOrders = true;

        switch (scenario)
        {
            case SqlUnitOfWorkScenarios.OperationSuccess:
            case SqlUnitOfWorkScenarios.OperationFailure:
            case SqlUnitOfWorkScenarios.OperationCancellation:
                await StageRowsAsync(operationId, cancellationToken);
                if (scenario == SqlUnitOfWorkScenarios.OperationFailure)
                    throw new InvalidOperationException("示例操作在写入后主动失败。");
                if (scenario == SqlUnitOfWorkScenarios.OperationCancellation)
                {
                    _operations.RecordProcessStep(operationId, operationId, "operation",
                        "operation-waiting-for-cancellation");
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                break;

            case SqlUnitOfWorkScenarios.OperationEfTrackedOnly:
                EnsureEfBridge();
                queryOrders = false;
                _dbContext.OrderRecords.Add(new OrderRecord
                {
                    Id = $"{operationId}:ef",
                    OperationId = operationId,
                    Layer = "ef-operation",
                    Sequence = 2,
                    Value = "tracked-only"
                });
                break;

            case SqlUnitOfWorkScenarios.OperationCapSuccess:
            case SqlUnitOfWorkScenarios.OperationCapEfFirstSuccess:
            case SqlUnitOfWorkScenarios.OperationCapFailure:
            case SqlUnitOfWorkScenarios.OperationCapCancellation:
                _operations.RecordProcessStep(operationId, operationId, "operation", "cap-staging-started");
                await StageCapOperationAsync(operationId, scenario, cancellationToken);
                if (scenario == SqlUnitOfWorkScenarios.OperationCapFailure)
                    throw new InvalidOperationException("CAP operation 在双库 Outbox 写入后失败。");
                if (scenario == SqlUnitOfWorkScenarios.OperationCapCancellation)
                {
                    _operations.RecordProcessStep(operationId, operationId, "operation",
                        "operation-waiting-for-cancellation");
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                break;

            default:
                throw new ArgumentException($"未知业务操作场景：{scenario}", nameof(scenario));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var orderCount = queryOrders ? await _orders.CountAsync(operationId, cancellationToken) : 0;
        var reportingCount = scenario is SqlUnitOfWorkScenarios.OperationCapSuccess or
            SqlUnitOfWorkScenarios.OperationCapEfFirstSuccess
            ? await _reporting.CountAsync(operationId, cancellationToken)
            : 0;
        return new ScenarioExecutionResult(operationId, scenario, scope.Id, false,
            orderCount, orderCount, reportingCount, Detail: _options.EfBridgeEnabled
                ? "Dapper 写入与 EF tracked-only 变更由操作 root 在返回前统一保存。"
                : "Dapper 写入由操作 root 在返回前统一提交。", ExecutionIdentity: identity);
    }

    private async Task StageCapOperationAsync(string operationId, string scenario,
        CancellationToken cancellationToken)
    {
        if (!_options.CapMySqlMode)
            throw new InvalidOperationException("CAP operation 需要以 SqlUow:Storage=CapMySqlEfBridge 启动。");
        if (!_options.EfBridgeEnabled)
            throw new InvalidOperationException("CAP operation 需要启用 EfBridge。");

        var publisher = _messagePublisher ?? throw new InvalidOperationException(
            "当前宿主未注册 CAP Uow 消息发布入口。");
        if (scenario == SqlUnitOfWorkScenarios.OperationCapEfFirstSuccess)
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

        await StageRowsAsync(operationId, cancellationToken);
        await _reporting.InsertAsync(operationId, "operation-report", cancellationToken);

        var scope = _scopeManager.Current ?? throw new InvalidOperationException(
            "CAP operation 要求活动的业务 Uow root。");
        var traceId = TraceIdContext.Current?.TraceId ?? operationId;
        foreach (var source in new[] { "orders", "reporting" })
        {
            var eventId = $"{operationId}:{source}-operation";
            var topic = source == "orders" ? SqlUnitOfWorkEventTopics.Orders :
                SqlUnitOfWorkEventTopics.Reporting;
            var payload = new SqlUnitOfWorkEventPayload(operationId, eventId, traceId,
                scope.Id.ToString("N"), source, scenario);
            var message = new MessageEvent<SqlUnitOfWorkEventPayload>(payload)
            {
                Id = eventId,
                Name = topic,
                Callback = string.Empty,
                Send = false
            };
            _operations.RecordCapEvent(operationId, scenario, eventId, traceId, payload.RootId, source, topic);
            _operations.MarkPublisherCallStarted(operationId, eventId, source);
            await publisher.PublishAsync(source, message, cancellationToken);
            _operations.MarkPublisherReturned(operationId, eventId, source);
            if (source == "orders")
                await _orders.RegisterCapBusinessEventAsync(payload, topic, cancellationToken);
            else
                await _reporting.RegisterCapBusinessEventAsync(payload, topic, cancellationToken);
        }
    }

    private async Task StageRowsAsync(string operationId, CancellationToken cancellationToken)
    {
        await _orders.InsertAsync($"{operationId}:operation", operationId, "operation", 1,
            "operation", cancellationToken);
        if (!_options.EfBridgeEnabled)
            return;

        _dbContext.OrderRecords.Add(new OrderRecord
        {
            Id = $"{operationId}:ef",
            OperationId = operationId,
            Layer = "ef-operation",
            Sequence = 2,
            Value = "tracked-only"
        });
    }

    private void EnsureEfBridge()
    {
        if (!_options.EfBridgeEnabled)
            throw new InvalidOperationException("该场景要求宿主启用 EfBridge。");
    }

    private static long GetObjectIdentity(object value) => ObjectIdentities.GetValue(value,
        static _ => new ObjectIdentity(Interlocked.Increment(ref _nextObjectIdentity))).Value;

    private sealed record ObjectIdentity(long Value);
}
