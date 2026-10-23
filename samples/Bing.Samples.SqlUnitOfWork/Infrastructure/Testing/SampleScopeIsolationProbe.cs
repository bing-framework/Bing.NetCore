using System.Collections.Concurrent;
using System.Data;
using Bing.Data.Sql;
using Bing.Samples.SqlUnitOfWork.Application;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Scenarios;
using Bing.Samples.SqlUnitOfWork.Repositories;
using Bing.Uow;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Testing;

/// <summary>
/// 为 Sample 的端到端测试记录真实 scope 依赖和协调只读并发窗口。
/// </summary>
internal sealed class SampleScopeIsolationProbe
{
    private readonly object _sync = new();
    private readonly ConcurrentDictionary<string, SampleScopeObservation> _observations =
        new(StringComparer.Ordinal);
    private readonly AsyncLocal<SampleScopeObservation?> _current = new();
    private TaskCompletionSource<bool> _allReadOnlyScopesReady = NewSignal();
    private int _activeScopes;
    private int _maxActualParallelism;
    private int _requestedConcurrency;
    private int _readyScopeCount;
    private bool _concurrentReadWindowEnabled;

    internal void BeginConcurrentReadWindow(int requestedConcurrency)
    {
        if (requestedConcurrency < 2)
            throw new ArgumentOutOfRangeException(nameof(requestedConcurrency));

        lock (_sync)
        {
            _requestedConcurrency = requestedConcurrency;
            _readyScopeCount = 0;
            _maxActualParallelism = 0;
            _concurrentReadWindowEnabled = true;
            _allReadOnlyScopesReady = NewSignal();
        }
    }

    internal async Task<IReadOnlyList<SampleScopeObservation>> WaitForConcurrentReadScopesAsync(TimeSpan timeout)
    {
        Task readyTask;
        lock (_sync)
            readyTask = _allReadOnlyScopesReady.Task;

        using var cancellation = new CancellationTokenSource(timeout);
        await readyTask.WaitAsync(cancellation.Token);

        return _observations.Values
            .Where(item => item.Scenario == SqlUnitOfWorkScenarios.ScopeIsolationProbe)
            .OrderBy(item => item.OperationId, StringComparer.Ordinal)
            .Take(_requestedConcurrency)
            .ToArray();
    }

    internal SampleScopeConcurrencyMetrics GetConcurrencyMetrics()
    {
        lock (_sync)
        {
            return new SampleScopeConcurrencyMetrics(
                _requestedConcurrency,
                _maxActualParallelism);
        }
    }

    internal SampleScopeObservation GetObservation(string operationId) =>
        _observations.TryGetValue(operationId, out var observation)
            ? observation
            : throw new InvalidOperationException($"找不到操作 {operationId} 的真实 scope 观察记录。");

    internal ScopeObservationLease Begin(string operationId, string scenario) =>
        new(this, operationId, scenario);

    internal IDbConnection CreateSqliteConnection(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.Equals(Path.GetFileName(dataSource), "orders.db", StringComparison.OrdinalIgnoreCase))
            _current.Value?.SetOrdersConnection(connection);
        return connection;
    }

    internal void ReleaseReadOnlyScope(string operationId)
    {
        if (_observations.TryGetValue(operationId, out var observation))
            observation.ReleaseReadOnlyGate();
    }

    internal void ReleaseAllReadOnlyScopes()
    {
        foreach (var observation in _observations.Values)
            observation.ReleaseReadOnlyGate();
    }

    internal void MarkDependencyScopeDisposed(string operationId)
    {
        if (_observations.TryGetValue(operationId, out var observation))
            observation.MarkDependencyScopeDisposed();
    }

    internal void RecordQueryState(string operationId, string role, string stage, string sql,
        IReadOnlyDictionary<string, object> parameters)
    {
        if (_observations.TryGetValue(operationId, out var observation) == false)
            return;

        observation.RecordQueryState(role, stage, sql, parameters);
    }

    internal void RecordQueryRows(string operationId, string role, string stage, IEnumerable<OrderQueryRow> rows)
    {
        if (_observations.TryGetValue(operationId, out var observation))
            observation.RecordQueryRows(role, stage, rows);
    }

    private void Attach(ScopeObservationLease lease, IUnitOfWorkScope scope,
        IUnitOfWorkScopeManager scopeManager, ISqlQuery applicationQuery, ISqlQuery repositoryQuery,
        SampleUnitOfWorkDbContext context, bool efBridgeEnabled)
    {
        var currentScope = scopeManager.Current;
        var observation = new SampleScopeObservation(
            lease.OperationId,
            lease.Scenario,
            scope,
            scopeManager,
            applicationQuery,
            repositoryQuery,
            context,
            efBridgeEnabled,
            ReferenceEquals(scope, currentScope));

        if (!_observations.TryAdd(lease.OperationId, observation))
            throw new InvalidOperationException($"操作 {lease.OperationId} 已有 scope 观察记录。");

        lease.Observation = observation;
        lease.PreviousObservation = _current.Value;
        _current.Value = observation;
        var activeScopes = Interlocked.Increment(ref _activeScopes);

        lock (_sync)
        {
            if (_concurrentReadWindowEnabled)
                _maxActualParallelism = Math.Max(_maxActualParallelism, activeScopes);
        }

        if (efBridgeEnabled)
            observation.SetOrdersConnection(context.Database.GetDbConnection());
    }

    private async Task WaitAtReadOnlyGateAsync(SampleScopeObservation observation, CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> allReady;
        lock (_sync)
        {
            if (!_concurrentReadWindowEnabled ||
                observation.Scenario != SqlUnitOfWorkScenarios.ScopeIsolationProbe)
                return;

            _readyScopeCount++;
            if (_readyScopeCount >= _requestedConcurrency)
                _allReadOnlyScopesReady.TrySetResult(true);
            allReady = _allReadOnlyScopesReady;
        }

        await allReady.Task.WaitAsync(cancellationToken);
        await observation.WaitForReadOnlyReleaseAsync(cancellationToken);
    }

    private void Complete(ScopeObservationLease lease)
    {
        if (lease.Observation == null)
            return;

        _current.Value = lease.PreviousObservation;
        lease.Observation.MarkUnitOfWorkScopeDisposed();
        Interlocked.Decrement(ref _activeScopes);
    }

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal sealed class ScopeObservationLease : IDisposable
    {
        private readonly SampleScopeIsolationProbe _probe;
        private int _disposed;

        internal ScopeObservationLease(SampleScopeIsolationProbe probe, string operationId, string scenario)
        {
            _probe = probe;
            OperationId = operationId;
            Scenario = scenario;
        }

        internal string OperationId { get; }
        internal string Scenario { get; }
        internal SampleScopeObservation? Observation { get; set; }
        internal SampleScopeObservation? PreviousObservation { get; set; }

        internal void Attach(IUnitOfWorkScope scope, IUnitOfWorkScopeManager scopeManager,
            ISqlQuery applicationQuery, ISqlQuery repositoryQuery, SampleUnitOfWorkDbContext context,
            bool efBridgeEnabled) =>
            _probe.Attach(this, scope, scopeManager, applicationQuery, repositoryQuery, context, efBridgeEnabled);

        internal void RecordQueryState(string role, string stage, string sql,
            IReadOnlyDictionary<string, object> parameters) =>
            _probe.RecordQueryState(OperationId, role, stage, sql, parameters);

        internal void RecordQueryRows(string role, string stage, IEnumerable<OrderQueryRow> rows) =>
            _probe.RecordQueryRows(OperationId, role, stage, rows);

        internal Task WaitAtReadOnlyGateAsync(CancellationToken cancellationToken) =>
            Observation == null
                ? Task.CompletedTask
                : _probe.WaitAtReadOnlyGateAsync(Observation, cancellationToken);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _probe.Complete(this);
        }
    }
}

internal sealed class SampleScopeObservation
{
    private readonly object _sync = new();
    private readonly List<SampleQueryStateSnapshot> _queryStates = new();
    private readonly List<SampleQueryRowsSnapshot> _queryRows = new();
    private readonly TaskCompletionSource<bool> _readOnlyRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _unitOfWorkScopeDisposed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _dependencyScopeDisposed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IDbConnection? _ordersConnection;
    private int _unitOfWorkScopeDisposedFlag;
    private int _dependencyScopeDisposedFlag;

    internal SampleScopeObservation(string operationId, string scenario, IUnitOfWorkScope scope,
        IUnitOfWorkScopeManager scopeManager, ISqlQuery applicationQuery, ISqlQuery repositoryQuery,
        SampleUnitOfWorkDbContext context, bool efBridgeEnabled, bool currentScopeMatched)
    {
        OperationId = operationId;
        Scenario = scenario;
        ScopeId = scope.Id;
        Scope = scope;
        ScopeManager = scopeManager;
        ApplicationQuery = applicationQuery;
        RepositoryQuery = repositoryQuery;
        DbContext = context;
        EfBridgeEnabled = efBridgeEnabled;
        CurrentScopeMatched = currentScopeMatched;
        ContextConnection = efBridgeEnabled ? context.Database.GetDbConnection() : null;
        if (ContextConnection != null)
            SetOrdersConnection(ContextConnection);
    }

    internal string OperationId { get; }
    internal string Scenario { get; }
    internal Guid ScopeId { get; }
    internal IUnitOfWorkScope Scope { get; }
    internal IUnitOfWorkScopeManager ScopeManager { get; }
    internal ISqlQuery ApplicationQuery { get; }
    internal ISqlQuery RepositoryQuery { get; }
    internal SampleUnitOfWorkDbContext DbContext { get; }
    internal bool EfBridgeEnabled { get; }
    internal bool CurrentScopeMatched { get; }
    internal IDbConnection? ContextConnection { get; }
    internal IDbConnection? OrdersConnection
    {
        get
        {
            lock (_sync)
                return _ordersConnection;
        }
    }

    internal bool IsUnitOfWorkScopeDisposed => Volatile.Read(ref _unitOfWorkScopeDisposedFlag) == 1;
    internal bool IsDependencyScopeDisposed => Volatile.Read(ref _dependencyScopeDisposedFlag) == 1;
    internal Task UnitOfWorkScopeDisposedTask => _unitOfWorkScopeDisposed.Task;
    internal Task DependencyScopeDisposedTask => _dependencyScopeDisposed.Task;

    internal IReadOnlyList<SampleQueryStateSnapshot> QueryStates
    {
        get
        {
            lock (_sync)
                return _queryStates.ToArray();
        }
    }

    internal IReadOnlyList<SampleQueryRowsSnapshot> QueryRows
    {
        get
        {
            lock (_sync)
                return _queryRows.ToArray();
        }
    }

    internal void SetOrdersConnection(IDbConnection connection)
    {
        lock (_sync)
        {
            if (_ordersConnection != null && !ReferenceEquals(_ordersConnection, connection))
                throw new InvalidOperationException("同一真实 scope 的 orders Query 使用了不同连接对象。");
            _ordersConnection = connection;
        }
    }

    internal void RecordQueryState(string role, string stage, string sql,
        IReadOnlyDictionary<string, object> parameters)
    {
        var snapshot = new SampleQueryStateSnapshot(
            role,
            stage,
            sql,
            parameters.ToDictionary(item => item.Key, item => (object?)item.Value, StringComparer.OrdinalIgnoreCase));
        lock (_sync)
            _queryStates.Add(snapshot);
    }

    internal void RecordQueryRows(string role, string stage, IEnumerable<OrderQueryRow> rows)
    {
        var snapshot = rows.Select(row => new OrderQueryRow
        {
            Id = row.Id,
            OperationId = row.OperationId,
            Layer = row.Layer,
            Sequence = row.Sequence,
            Value = row.Value
        }).ToArray();
        lock (_sync)
            _queryRows.Add(new SampleQueryRowsSnapshot(role, stage, snapshot));
    }

    internal void ReleaseReadOnlyGate() => _readOnlyRelease.TrySetResult(true);

    internal Task WaitForReadOnlyReleaseAsync(CancellationToken cancellationToken) =>
        _readOnlyRelease.Task.WaitAsync(cancellationToken);

    internal void MarkUnitOfWorkScopeDisposed()
    {
        if (Interlocked.Exchange(ref _unitOfWorkScopeDisposedFlag, 1) == 0)
            _unitOfWorkScopeDisposed.TrySetResult(true);
    }

    internal void MarkDependencyScopeDisposed()
    {
        if (Interlocked.Exchange(ref _dependencyScopeDisposedFlag, 1) == 0)
            _dependencyScopeDisposed.TrySetResult(true);
    }
}

internal sealed record SampleQueryStateSnapshot(
    string Role,
    string Stage,
    string Sql,
    IReadOnlyDictionary<string, object?> Parameters);

internal sealed record SampleQueryRowsSnapshot(
    string Role,
    string Stage,
    IReadOnlyList<OrderQueryRow> Rows);

internal sealed record SampleScopeConcurrencyMetrics(
    int RequestedConcurrency,
    int MaxActualParallelism);

/// <summary>
/// 在测试宿主中最后释放的 scoped marker，用于确认查询和 context 已先释放。
/// </summary>
internal sealed class SampleScopeLifetime : IDisposable, IAsyncDisposable
{
    private SampleScopeIsolationProbe? _probe;
    private string? _operationId;
    private int _disposed;

    internal void Attach(SampleScopeIsolationProbe? probe, string operationId)
    {
        _probe = probe;
        _operationId = operationId;
    }

    public void Dispose() => MarkDisposed();

    public ValueTask DisposeAsync()
    {
        MarkDisposed();
        return ValueTask.CompletedTask;
    }

    private void MarkDisposed()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _probe != null && _operationId != null)
            _probe.MarkDependencyScopeDisposed(_operationId);
    }
}
