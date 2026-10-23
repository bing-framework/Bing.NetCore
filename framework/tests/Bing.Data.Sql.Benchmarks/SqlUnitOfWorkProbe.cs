using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Text.Json;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Bing.Uow;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// 通过默认 Dapper 执行链执行有界的 Uow 热路径与双文件 SQLite 探针。
/// </summary>
internal static class SqlUnitOfWorkProbe
{
    private static readonly TimeSpan TimeBudget = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 运行一次固定预算探针并将原始指标写入 UTF-8 JSON。
    /// </summary>
    /// <param name="outputPath">结果文件路径。</param>
    /// <returns>成功时为 0；超时或失败时为非零值。</returns>
    public static int Run(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("探针结果路径不能为空。", nameof(outputPath));

        var stopwatch = Stopwatch.StartNew();
        var report = new ProbeReport
        {
            Candidate = Environment.GetEnvironmentVariable("BING_SQL_UOW_PROBE_CANDIDATE") ??
                        "review-fix-round-1-worktree; see watchdog result and task candidate manifest",
            HarnessVersion = "review-fix-round-1",
            Runtime = RuntimeInformation.FrameworkDescription,
            TargetFramework = ".NET 8.0",
            RequestedConcurrency = 1,
            MaximumActualParallelism = 1,
            MaximumQueuedRequests = 0,
            TimeBudgetSeconds = (int)TimeBudget.TotalSeconds,
            ThresholdStatus = "BLOCKED_APPROVAL"
        };
        ProbeContext context = null;
        try
        {
            context = new ProbeContext();
            RunLeaseProbe(context, stopwatch, report);
            RunE2EProbe(context, stopwatch, report);
            report.Status = "EXPECTED_SUCCESS";
            return WriteReport(outputPath, report, stopwatch, context, 0);
        }
        catch (TimeoutException exception)
        {
            report.Status = "TIMEOUT_BOUNDARY";
            report.Failure = exception.ToString();
            return WriteReport(outputPath, report, stopwatch, context, 2);
        }
        catch (Exception exception)
        {
            report.Status = "FAILED";
            report.Failure = exception.ToString();
            return WriteReport(outputPath, report, stopwatch, context, 1);
        }
        finally
        {
            context?.Dispose();
        }
    }

    private static void RunLeaseProbe(ProbeContext context, Stopwatch stopwatch, ProbeReport report)
    {
        AddMeasurement(report, Measure("resource_lookup_lease_warmup_100", 100, context, stopwatch,
            () => RunLeaseBatch(context, 100, stopwatch)));
        for (var round = 1; round <= 3; round++)
        {
            var index = round;
            AddMeasurement(report, Measure($"resource_lookup_lease_round_{index}_1000", 1000, context, stopwatch,
                () => RunLeaseBatch(context, 1000, stopwatch)));
        }
    }

    private static void RunE2EProbe(ProbeContext context, Stopwatch stopwatch, ProbeReport report)
    {
        MeasureScenario("sqlite_e2e_no_uow", 10, 100, context, stopwatch,
            () => context.ReadAndMergeWithoutUnitOfWork());
        MeasureScenario("sqlite_e2e_single_database_transaction_uow", 10, 100, context, stopwatch,
            () => context.ReadAndWriteWithinOneUnitOfWork());
        MeasureScenario("sqlite_e2e_two_database_nontransactional_summary", 10, 100, context, stopwatch,
            () => context.ReadTwoDatabasesWithoutTransactions());

        void MeasureScenario(string name, int warmupCount, int iterations, ProbeContext probeContext,
            Stopwatch timer, Func<int> operation)
        {
            AddMeasurement(report, Measure(name + "_warmup_" + warmupCount, warmupCount, probeContext, timer, () =>
            {
                for (var index = 0; index < warmupCount; index++)
                {
                    CheckBudget(timer);
                    operation();
                }
            }));
            for (var round = 1; round <= 3; round++)
            {
                var roundIndex = round;
                AddMeasurement(report, Measure(name + "_round_" + roundIndex + "_" + iterations,
                    iterations, probeContext, timer, () =>
                {
                    for (var index = 0; index < iterations; index++)
                    {
                        CheckBudget(timer);
                        operation();
                    }
                }));
            }
        }
    }

    private static ProbeMeasurement Measure(string name, int expectedOperations, ProbeContext context,
        Stopwatch stopwatch, Action operation)
    {
        CheckBudget(stopwatch);
        var counterStart = context.Counters.Snapshot();
        var allocatedStart = GC.GetTotalAllocatedBytes(precise: true);
        var elapsedStart = stopwatch.Elapsed;
        var exceptionCount = 0;
        var completed = 0;
        try
        {
            operation();
            completed = expectedOperations;
        }
        catch
        {
            exceptionCount++;
            throw;
        }
        var counterEnd = context.Counters.Snapshot();
        return new ProbeMeasurement
        {
            Name = name,
            ExpectedOperations = expectedOperations,
            CompletedOperations = completed,
            ElapsedMilliseconds = (stopwatch.Elapsed - elapsedStart).TotalMilliseconds,
            AllocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedStart,
            AllocationScope = "process_gc_total",
            ConnectionsCreated = counterEnd.ConnectionsCreated - counterStart.ConnectionsCreated,
            ConnectionsOpened = counterEnd.ConnectionsOpened - counterStart.ConnectionsOpened,
            ConnectionsClosed = counterEnd.ConnectionsClosed - counterStart.ConnectionsClosed,
            ConnectionsDisposed = counterEnd.ConnectionsDisposed - counterStart.ConnectionsDisposed,
            TransactionsBegun = counterEnd.TransactionsBegun - counterStart.TransactionsBegun,
            TransactionsCommitted = counterEnd.TransactionsCommitted - counterStart.TransactionsCommitted,
            TransactionsRolledBack = counterEnd.TransactionsRolledBack - counterStart.TransactionsRolledBack,
            TransactionsDisposed = counterEnd.TransactionsDisposed - counterStart.TransactionsDisposed,
            Exceptions = exceptionCount,
            PeakOpenConnections = counterEnd.PeakOpenConnections,
            RequestedConcurrency = 1,
            MaximumActualParallelism = 1,
            MaximumQueuedRequests = 0
        };
    }

    private static void AddMeasurement(ProbeReport report, ProbeMeasurement measurement) =>
        report.Measurements.Add(measurement);

    private static void RunLeaseBatch(ProbeContext context, int iterations, Stopwatch stopwatch)
    {
        using var root = context.ScopeManager.Begin();
        using (var first = context.QueryFactory.Create("first"))
            EnsureOne(first.Sql("Select 1").Scalar<int>());
        for (var index = 0; index < iterations; index++)
        {
            CheckBudget(stopwatch);
            using var query = context.QueryFactory.Create("first");
            EnsureOne(query.Sql("Select 1").Scalar<int>());
        }
        root.Complete();
    }

    private static void EnsureOne(int value)
    {
        if (value != 1)
            throw new InvalidOperationException($"SQLite Uow probe expected 1 but received {value}.");
    }

    private static void CheckBudget(Stopwatch stopwatch)
    {
        if (stopwatch.Elapsed >= TimeBudget)
            throw new TimeoutException($"Uow performance probe exceeded its {TimeBudget.TotalSeconds:0}s wall-clock budget.");
    }

    private static int WriteReport(string outputPath, ProbeReport report, Stopwatch stopwatch,
        ProbeContext context, int exitCode)
    {
        report.ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        if (context != null)
            report.FinalCounters = context.Counters.Snapshot();
        var path = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.WriteLine(JsonSerializer.Serialize(report));
        return exitCode;
    }

    private sealed class ProbeContext : IDisposable
    {
        private readonly string _directory;
        private readonly string _firstConnectionString;
        private readonly string _secondConnectionString;
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public ProbeContext()
        {
            _directory = Path.Combine(Path.GetTempPath(), "bing-uow-probe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _firstConnectionString = CreateDatabase("first.db", "first-seed");
            _secondConnectionString = CreateDatabase("second.db", "second-seed");
            Counters = new ProbeCounters();
            var connectionFactory = new ProbeConnectionFactory(Counters);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSqlCore();
            services.AddSqliteProvider();
            services.AddSqlDataSource("first", DatabaseType.Sqlite, _firstConnectionString);
            services.AddSqlDataSource("second", DatabaseType.Sqlite, _secondConnectionString);
            services.AddUnitOfWorkScopes();
            services.Replace(ServiceDescriptor.Singleton<ISqlDbConnectionFactoryResolver>(connectionFactory));
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateScope();
            QueryFactory = _scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
            ExecutorFactory = _scope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>();
            ScopeManager = _scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        }

        public ProbeCounters Counters { get; }

        public ISqlQueryFactory QueryFactory { get; }

        public ISqlExecutorFactory ExecutorFactory { get; }

        public IUnitOfWorkScopeManager ScopeManager { get; }

        public int ReadAndMergeWithoutUnitOfWork()
        {
            using var first = QueryFactory.Create("first");
            using var second = QueryFactory.Create("second");
            return ReadCount(first) + ReadCount(second);
        }

        public int ReadAndWriteWithinOneUnitOfWork()
        {
            using var root = ScopeManager.Begin();
            using var query = QueryFactory.Create("first");
            using var executor = ExecutorFactory.Create("first");
            var before = ReadCount(query);
            executor.ExecuteSql("Insert Into samples(Name) Values (@name)", new { name = Guid.NewGuid().ToString("N") });
            root.Complete();
            return before;
        }

        public int ReadTwoDatabasesWithoutTransactions()
        {
            using var root = ScopeManager.Begin(new UnitOfWorkScopeOptions { IsTransactional = false });
            using var first = QueryFactory.Create("first");
            using var second = QueryFactory.Create("second");
            var result = ReadCount(first) + ReadCount(second);
            root.Complete();
            return result;
        }

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private static int ReadCount(ISqlQuery query) => query.Sql("Select Count(*) From samples").Scalar<int>();

        private string CreateDatabase(string fileName, string seed)
        {
            var path = Path.Combine(_directory, fileName);
            var connectionString = $"Data Source={path};Mode=ReadWriteCreate;Pooling=False";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "Create Table samples (Id Integer Primary Key, Name Text Not Null);";
            command.ExecuteNonQuery();
            command.CommandText = "Insert Into samples (Name) Values ($name);";
            command.Parameters.AddWithValue("$name", seed);
            command.ExecuteNonQuery();
            return connectionString;
        }
    }

    private sealed class ProbeConnectionFactory : ISqlDbConnectionFactoryResolver
    {
        private readonly ProbeCounters _counters;

        public ProbeConnectionFactory(ProbeCounters counters) => _counters = counters;

        public IDbConnection Create(string providerKey, string connectionString)
        {
            _counters.RecordCreated();
            return new ProbeConnection(connectionString, _counters);
        }
    }

    private sealed class ProbeConnection : IDbConnection
    {
        private readonly SqliteConnection _inner;
        private readonly ProbeCounters _counters;

        public ProbeConnection(string connectionString, ProbeCounters counters)
        {
            _inner = new SqliteConnection(connectionString);
            _counters = counters;
        }

        public string ConnectionString { get => _inner.ConnectionString; set => _inner.ConnectionString = value; }
        public int ConnectionTimeout => _inner.ConnectionTimeout;
        public string Database => _inner.Database;
        public ConnectionState State => _inner.State;
        public SqliteConnection Inner => _inner;

        public IDbTransaction BeginTransaction()
        {
            return new ProbeTransaction(_inner.BeginTransaction(), _counters);
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return new ProbeTransaction(_inner.BeginTransaction(il), _counters);
        }

        public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

        public void Close()
        {
            var wasOpen = State == ConnectionState.Open;
            _inner.Close();
            if (wasOpen)
                _counters.RecordClosed();
        }

        public IDbCommand CreateCommand() => new ProbeCommand(_inner.CreateCommand(), this);

        public void Open()
        {
            _inner.Open();
            _counters.RecordOpened();
        }

        public void Dispose()
        {
            var wasOpen = State == ConnectionState.Open;
            _inner.Dispose();
            _counters.RecordDisposed(wasOpen);
        }
    }

    private sealed class ProbeTransaction : IDbTransaction
    {
        private readonly SqliteTransaction _inner;
        private readonly ProbeCounters _counters;
        private bool _disposed;

        public ProbeTransaction(IDbTransaction inner, ProbeCounters counters)
        {
            _inner = (SqliteTransaction)inner;
            _counters = counters;
            _counters.RecordTransactionBegin();
        }

        public SqliteTransaction Inner => _inner;
        public IDbConnection Connection => _inner.Connection;
        public IsolationLevel IsolationLevel => _inner.IsolationLevel;

        public void Commit()
        {
            _inner.Commit();
            _counters.RecordTransactionCommit();
        }

        public void Rollback()
        {
            _inner.Rollback();
            _counters.RecordTransactionRollback();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _inner.Dispose();
            _disposed = true;
            _counters.RecordTransactionDisposed();
        }
    }

    private sealed class ProbeCommand : IDbCommand
    {
        private readonly IDbCommand _inner;
        private readonly ProbeConnection _connection;

        public ProbeCommand(IDbCommand inner, ProbeConnection connection)
        {
            _inner = inner;
            _connection = connection;
        }

        public string CommandText { get => _inner.CommandText; set => _inner.CommandText = value; }
        public int CommandTimeout { get => _inner.CommandTimeout; set => _inner.CommandTimeout = value; }
        public CommandType CommandType { get => _inner.CommandType; set => _inner.CommandType = value; }
        public IDbConnection Connection
        {
            get => _connection;
            set => _inner.Connection = value is ProbeConnection probe ? probe.Inner : (SqliteConnection)value;
        }
        public IDataParameterCollection Parameters => _inner.Parameters;
        public IDbTransaction Transaction
        {
            get => _inner.Transaction;
            set => _inner.Transaction = value is ProbeTransaction probe ? probe.Inner : value;
        }
        public UpdateRowSource UpdatedRowSource { get => _inner.UpdatedRowSource; set => _inner.UpdatedRowSource = value; }

        public void Cancel() => _inner.Cancel();
        public IDbDataParameter CreateParameter() => _inner.CreateParameter();
        public int ExecuteNonQuery() => _inner.ExecuteNonQuery();
        public IDataReader ExecuteReader() => _inner.ExecuteReader();
        public IDataReader ExecuteReader(CommandBehavior behavior) => _inner.ExecuteReader(behavior);
        public object ExecuteScalar() => _inner.ExecuteScalar();
        public void Prepare() => _inner.Prepare();
        public void Dispose() => _inner.Dispose();
    }

    private sealed class ProbeCounters
    {
        private long _connectionsCreated;
        private long _connectionsOpened;
        private long _connectionsClosed;
        private long _connectionsDisposed;
        private long _transactionsBegun;
        private long _transactionsCommitted;
        private long _transactionsRolledBack;
        private long _transactionsDisposed;
        private long _openConnections;
        private long _peakOpenConnections;

        public ProbeCounterSnapshot Snapshot() => new()
        {
            ConnectionsCreated = Interlocked.Read(ref _connectionsCreated),
            ConnectionsOpened = Interlocked.Read(ref _connectionsOpened),
            ConnectionsClosed = Interlocked.Read(ref _connectionsClosed),
            ConnectionsDisposed = Interlocked.Read(ref _connectionsDisposed),
            TransactionsBegun = Interlocked.Read(ref _transactionsBegun),
            TransactionsCommitted = Interlocked.Read(ref _transactionsCommitted),
            TransactionsRolledBack = Interlocked.Read(ref _transactionsRolledBack),
            TransactionsDisposed = Interlocked.Read(ref _transactionsDisposed),
            CurrentOpenConnections = Interlocked.Read(ref _openConnections),
            PeakOpenConnections = Interlocked.Read(ref _peakOpenConnections)
        };

        public void RecordCreated() => Interlocked.Increment(ref _connectionsCreated);

        public void RecordOpened()
        {
            Interlocked.Increment(ref _connectionsOpened);
            var open = Interlocked.Increment(ref _openConnections);
            long peak;
            while (open > (peak = Interlocked.Read(ref _peakOpenConnections)) &&
                   Interlocked.CompareExchange(ref _peakOpenConnections, open, peak) != peak)
            {
            }
        }

        public void RecordClosed()
        {
            Interlocked.Increment(ref _connectionsClosed);
            Interlocked.Decrement(ref _openConnections);
        }

        public void RecordDisposed(bool wasOpen)
        {
            Interlocked.Increment(ref _connectionsDisposed);
            if (wasOpen)
                RecordClosed();
        }

        public void RecordTransactionBegin() => Interlocked.Increment(ref _transactionsBegun);

        public void RecordTransactionCommit() => Interlocked.Increment(ref _transactionsCommitted);

        public void RecordTransactionRollback() => Interlocked.Increment(ref _transactionsRolledBack);

        public void RecordTransactionDisposed() => Interlocked.Increment(ref _transactionsDisposed);
    }

    private sealed class ProbeReport
    {
        public string Candidate { get; set; }
        public string HarnessVersion { get; set; }
        public string Runtime { get; set; }
        public string TargetFramework { get; set; }
        public string Status { get; set; }
        public int TimeBudgetSeconds { get; set; }
        public double ElapsedMilliseconds { get; set; }
        public int RequestedConcurrency { get; set; }
        public int MaximumActualParallelism { get; set; }
        public int MaximumQueuedRequests { get; set; }
        public string ThresholdStatus { get; set; }
        public string AllocationScope { get; set; } = "process_gc_total";
        public string Failure { get; set; }
        public List<ProbeMeasurement> Measurements { get; } = new();
        public ProbeCounterSnapshot FinalCounters { get; set; }
    }

    private sealed class ProbeMeasurement
    {
        public string Name { get; set; }
        public int ExpectedOperations { get; set; }
        public int CompletedOperations { get; set; }
        public double ElapsedMilliseconds { get; set; }
        public long AllocatedBytes { get; set; }
        public string AllocationScope { get; set; }
        public long ConnectionsCreated { get; set; }
        public long ConnectionsOpened { get; set; }
        public long ConnectionsClosed { get; set; }
        public long ConnectionsDisposed { get; set; }
        public long TransactionsBegun { get; set; }
        public long TransactionsCommitted { get; set; }
        public long TransactionsRolledBack { get; set; }
        public long TransactionsDisposed { get; set; }
        public int Exceptions { get; set; }
        public long PeakOpenConnections { get; set; }
        public int RequestedConcurrency { get; set; }
        public int MaximumActualParallelism { get; set; }
        public int MaximumQueuedRequests { get; set; }
    }

    private sealed class ProbeCounterSnapshot
    {
        public long ConnectionsCreated { get; set; }
        public long ConnectionsOpened { get; set; }
        public long ConnectionsClosed { get; set; }
        public long ConnectionsDisposed { get; set; }
        public long TransactionsBegun { get; set; }
        public long TransactionsCommitted { get; set; }
        public long TransactionsRolledBack { get; set; }
        public long TransactionsDisposed { get; set; }
        public long CurrentOpenConnections { get; set; }
        public long PeakOpenConnections { get; set; }
    }
}
