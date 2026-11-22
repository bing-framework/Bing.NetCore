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
    /// <summary>
    /// 定义工作单元探针的时间预算。
    /// </summary>
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

    /// <summary>
    /// 运行工作单元资源租约探测并记录测量值。
    /// </summary>
    /// <param name="context">SQL 工作单元探测上下文。</param>
    /// <param name="stopwatch">测量耗时及检查时间预算的计时器。</param>
    /// <param name="report">接收探测测量结果的报告。</param>
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

    /// <summary>
    /// 运行 SQLite 工作单元端到端探测。
    /// </summary>
    /// <param name="context">SQL 工作单元探测上下文。</param>
    /// <param name="stopwatch">测量耗时及检查时间预算的计时器。</param>
    /// <param name="report">接收探测测量结果的报告。</param>
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

    /// <summary>
    /// 测量操作耗时、分配量及资源计数。
    /// </summary>
    /// <param name="name">注册或构造的测试服务、操作或场景名称。</param>
    /// <param name="expectedOperations">本次测量预期执行的操作数。</param>
    /// <param name="context">SQL 工作单元探测上下文。</param>
    /// <param name="stopwatch">测量耗时及检查时间预算的计时器。</param>
    /// <param name="operation">需要测量的同步操作。</param>
    /// <returns>操作耗时、分配量及资源计数的测量结果。</returns>
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

    /// <summary>
    /// 向探测报告添加测量结果。
    /// </summary>
    /// <param name="report">接收探测测量结果的报告。</param>
    /// <param name="measurement">需要加入报告的测量结果。</param>
    private static void AddMeasurement(ProbeReport report, ProbeMeasurement measurement) =>
        report.Measurements.Add(measurement);

    /// <summary>
    /// 批量执行工作单元资源租约访问。
    /// </summary>
    /// <param name="context">SQL 工作单元探测上下文。</param>
    /// <param name="iterations">重复执行的次数。</param>
    /// <param name="stopwatch">测量耗时及检查时间预算的计时器。</param>
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

    /// <summary>
    /// 验证 SQLite 探测结果为一。
    /// </summary>
    /// <param name="value">待验证或作为异步结果的数值。</param>
    private static void EnsureOne(int value)
    {
        if (value != 1)
            throw new InvalidOperationException($"SQLite Uow probe expected 1 but received {value}.");
    }

    /// <summary>
    /// 检查探测是否超过时间预算。
    /// </summary>
    /// <param name="stopwatch">测量耗时及检查时间预算的计时器。</param>
    private static void CheckBudget(Stopwatch stopwatch)
    {
        if (stopwatch.Elapsed >= TimeBudget)
            throw new TimeoutException($"Uow performance probe exceeded its {TimeBudget.TotalSeconds:0}s wall-clock budget.");
    }

    /// <summary>
    /// 写入探测报告并返回退出码。
    /// </summary>
    /// <param name="outputPath">探测报告的输出路径。</param>
    /// <param name="report">接收探测测量结果的报告。</param>
    /// <param name="stopwatch">测量耗时及检查时间预算的计时器。</param>
    /// <param name="context">SQL 工作单元探测上下文。</param>
    /// <param name="exitCode">探测进程的退出代码。</param>
    /// <returns>报告所记录的退出代码。</returns>
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

    /// <summary>
    /// 保存 SQL 工作单元探针使用的服务和资源状态。
    /// </summary>
    private sealed class ProbeContext : IDisposable
    {
        /// <summary>
        /// 保存探针使用的临时目录。
        /// </summary>
        private readonly string _directory;
        /// <summary>
        /// 保存探针第一个数据库的连接字符串。
        /// </summary>
        private readonly string _firstConnectionString;
        /// <summary>
        /// 保存探针第二个数据库的连接字符串。
        /// </summary>
        private readonly string _secondConnectionString;
        /// <summary>
        /// 保存探针使用的服务提供程序。
        /// </summary>
        private readonly ServiceProvider _provider;
        /// <summary>
        /// 保存探针使用的依赖注入作用域。
        /// </summary>
        private readonly IServiceScope _scope;

        /// <summary>
        /// 初始化一个 <see cref="ProbeContext" /> 类型的实例。
        /// </summary>
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

        /// <summary>
        /// 获取当前 SQL 工作单元探针的测量计数器。
        /// </summary>
        public ProbeCounters Counters { get; }

        /// <summary>
        /// 获取当前执行使用的 SQL 查询工厂。
        /// </summary>
        public ISqlQueryFactory QueryFactory { get; }

        /// <summary>
        /// 获取当前执行使用的 SQL 执行器工厂。
        /// </summary>
        public ISqlExecutorFactory ExecutorFactory { get; }

        /// <summary>
        /// 获取当前测试使用的工作单元作用域管理器。
        /// </summary>
        public IUnitOfWorkScopeManager ScopeManager { get; }

        /// <summary>
        /// 在无工作单元时合并两个查询的计数。
        /// </summary>
        /// <returns>两个测试查询的记录数之和。</returns>
        public int ReadAndMergeWithoutUnitOfWork()
        {
            using var first = QueryFactory.Create("first");
            using var second = QueryFactory.Create("second");
            return ReadCount(first) + ReadCount(second);
        }

        /// <summary>
        /// 在同一工作单元内读取并写入测试数据。
        /// </summary>
        /// <returns>写入前读取到的记录数。</returns>
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

        /// <summary>
        /// 在非事务工作单元中读取两个数据库。
        /// </summary>
        /// <returns>两个数据库的记录数之和。</returns>
        public int ReadTwoDatabasesWithoutTransactions()
        {
            using var root = ScopeManager.Begin(new UnitOfWorkScopeOptions { IsTransactional = false });
            using var first = QueryFactory.Create("first");
            using var second = QueryFactory.Create("second");
            var result = ReadCount(first) + ReadCount(second);
            root.Complete();
            return result;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        /// <summary>
        /// 读取测试表的记录数。
        /// </summary>
        /// <param name="query">待执行或构造的测试查询。</param>
        /// <returns>符合查询条件的记录数。</returns>
        private static int ReadCount(ISqlQuery query) => query.Sql("Select Count(*) From samples").Scalar<int>();

        /// <summary>
        /// 创建并初始化探测数据库。
        /// </summary>
        /// <param name="fileName">待创建的测试数据库文件名。</param>
        /// <param name="seed">初始化数据库时写入的种子值。</param>
        /// <returns>已初始化探测数据库的连接字符串。</returns>
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

    /// <summary>
    /// 创建并统计 SQL 工作单元探针的数据库连接。
    /// </summary>
    private sealed class ProbeConnectionFactory : ISqlDbConnectionFactoryResolver
    {
        /// <summary>
        /// 保存探针连接工厂共享的计数器。
        /// </summary>
        private readonly ProbeCounters _counters;

        /// <summary>
        /// 初始化一个 <see cref="ProbeConnectionFactory" /> 类型的实例。
        /// </summary>
        /// <param name="counters">用于汇总本次测试运行指标的计数器。</param>
        public ProbeConnectionFactory(ProbeCounters counters) => _counters = counters;

        /// <inheritdoc />
        public IDbConnection Create(string providerKey, string connectionString)
        {
            _counters.RecordCreated();
            return new ProbeConnection(connectionString, _counters);
        }
    }

    /// <summary>
    /// 记录 SQL 工作单元探针连接操作的数据库连接替身。
    /// </summary>
    private sealed class ProbeConnection : IDbConnection
    {
        /// <summary>
        /// 保存被包装的 SQLite 连接。
        /// </summary>
        private readonly SqliteConnection _inner;
        /// <summary>
        /// 保存包装连接共享的探针计数器。
        /// </summary>
        private readonly ProbeCounters _counters;

        /// <summary>
        /// 初始化一个 <see cref="ProbeConnection" /> 类型的实例。
        /// </summary>
        /// <param name="connectionString">用于建立数据库连接的连接字符串。</param>
        /// <param name="counters">用于汇总本次测试运行指标的计数器。</param>
        public ProbeConnection(string connectionString, ProbeCounters counters)
        {
            _inner = new SqliteConnection(connectionString);
            _counters = counters;
        }

        /// <inheritdoc />
        public string ConnectionString { get => _inner.ConnectionString; set => _inner.ConnectionString = value; }
        /// <inheritdoc />
        public int ConnectionTimeout => _inner.ConnectionTimeout;
        /// <inheritdoc />
        public string Database => _inner.Database;
        /// <inheritdoc />
        public ConnectionState State => _inner.State;
        /// <summary>
        /// 获取当前包装的内部实例。
        /// </summary>
        public SqliteConnection Inner => _inner;

        /// <inheritdoc />
        public IDbTransaction BeginTransaction()
        {
            return new ProbeTransaction(_inner.BeginTransaction(), _counters);
        }

        /// <inheritdoc />
        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return new ProbeTransaction(_inner.BeginTransaction(il), _counters);
        }

        /// <inheritdoc />
        public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

        /// <inheritdoc />
        public void Close()
        {
            var wasOpen = State == ConnectionState.Open;
            _inner.Close();
            if (wasOpen)
                _counters.RecordClosed();
        }

        /// <inheritdoc />
        public IDbCommand CreateCommand() => new ProbeCommand(_inner.CreateCommand(), this);

        /// <inheritdoc />
        public void Open()
        {
            _inner.Open();
            _counters.RecordOpened();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            var wasOpen = State == ConnectionState.Open;
            _inner.Dispose();
            _counters.RecordDisposed(wasOpen);
        }
    }

    /// <summary>
    /// 记录 SQL 工作单元探针事务操作的数据库事务替身。
    /// </summary>
    private sealed class ProbeTransaction : IDbTransaction
    {
        /// <summary>
        /// 保存被包装的 SQLite 事务。
        /// </summary>
        private readonly SqliteTransaction _inner;
        /// <summary>
        /// 保存包装事务共享的探针计数器。
        /// </summary>
        private readonly ProbeCounters _counters;
        /// <summary>
        /// 指示探针事务是否已释放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化一个 <see cref="ProbeTransaction" /> 类型的实例。
        /// </summary>
        /// <param name="inner">由当前包装器保留或转发的内部对象或异常。</param>
        /// <param name="counters">用于汇总本次测试运行指标的计数器。</param>
        public ProbeTransaction(IDbTransaction inner, ProbeCounters counters)
        {
            _inner = (SqliteTransaction)inner;
            _counters = counters;
            _counters.RecordTransactionBegin();
        }

        /// <summary>
        /// 获取当前包装的内部实例。
        /// </summary>
        public SqliteTransaction Inner => _inner;
        /// <inheritdoc />
        public IDbConnection Connection => _inner.Connection;
        /// <inheritdoc />
        public IsolationLevel IsolationLevel => _inner.IsolationLevel;

        /// <inheritdoc />
        public void Commit()
        {
            _inner.Commit();
            _counters.RecordTransactionCommit();
        }

        /// <inheritdoc />
        public void Rollback()
        {
            _inner.Rollback();
            _counters.RecordTransactionRollback();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
                return;
            _inner.Dispose();
            _disposed = true;
            _counters.RecordTransactionDisposed();
        }
    }

    /// <summary>
    /// 记录 SQL 工作单元探针命令执行状态的数据库命令替身。
    /// </summary>
    private sealed class ProbeCommand : IDbCommand
    {
        /// <summary>
        /// 保存被包装的数据库命令。
        /// </summary>
        private readonly IDbCommand _inner;
        /// <summary>
        /// 保存命令所属的探针连接。
        /// </summary>
        private readonly ProbeConnection _connection;

        /// <summary>
        /// 初始化一个 <see cref="ProbeCommand" /> 类型的实例。
        /// </summary>
        /// <param name="inner">由当前包装器保留或转发的内部对象或异常。</param>
        /// <param name="connection">当前实例使用的连接对象或连接配置。</param>
        public ProbeCommand(IDbCommand inner, ProbeConnection connection)
        {
            _inner = inner;
            _connection = connection;
        }

        /// <inheritdoc />
        public string CommandText { get => _inner.CommandText; set => _inner.CommandText = value; }
        /// <inheritdoc />
        public int CommandTimeout { get => _inner.CommandTimeout; set => _inner.CommandTimeout = value; }
        /// <inheritdoc />
        public CommandType CommandType { get => _inner.CommandType; set => _inner.CommandType = value; }
        /// <inheritdoc />
        public IDbConnection Connection
        {
            get => _connection;
            set => _inner.Connection = value is ProbeConnection probe ? probe.Inner : (SqliteConnection)value;
        }
        /// <inheritdoc />
        public IDataParameterCollection Parameters => _inner.Parameters;
        /// <inheritdoc />
        public IDbTransaction Transaction
        {
            get => _inner.Transaction;
            set => _inner.Transaction = value is ProbeTransaction probe ? probe.Inner : value;
        }
        /// <inheritdoc />
        public UpdateRowSource UpdatedRowSource { get => _inner.UpdatedRowSource; set => _inner.UpdatedRowSource = value; }

        /// <inheritdoc />
        public void Cancel() => _inner.Cancel();
        /// <inheritdoc />
        public IDbDataParameter CreateParameter() => _inner.CreateParameter();
        /// <inheritdoc />
        public int ExecuteNonQuery() => _inner.ExecuteNonQuery();
        /// <inheritdoc />
        public IDataReader ExecuteReader() => _inner.ExecuteReader();
        /// <inheritdoc />
        public IDataReader ExecuteReader(CommandBehavior behavior) => _inner.ExecuteReader(behavior);
        /// <inheritdoc />
        public object ExecuteScalar() => _inner.ExecuteScalar();
        /// <inheritdoc />
        public void Prepare() => _inner.Prepare();
        /// <inheritdoc />
        public void Dispose() => _inner.Dispose();
    }

    /// <summary>
    /// 统计 SQL 工作单元探针中的连接和事务生命周期操作。
    /// </summary>
    private sealed class ProbeCounters
    {
        /// <summary>
        /// 记录探针创建的连接数量。
        /// </summary>
        private long _connectionsCreated;
        /// <summary>
        /// 记录探针打开的连接数量。
        /// </summary>
        private long _connectionsOpened;
        /// <summary>
        /// 记录探针关闭的连接数量。
        /// </summary>
        private long _connectionsClosed;
        /// <summary>
        /// 记录探针释放的连接数量。
        /// </summary>
        private long _connectionsDisposed;
        /// <summary>
        /// 记录探针开始的事务数量。
        /// </summary>
        private long _transactionsBegun;
        /// <summary>
        /// 记录探针提交的事务数量。
        /// </summary>
        private long _transactionsCommitted;
        /// <summary>
        /// 记录探针回滚的事务数量。
        /// </summary>
        private long _transactionsRolledBack;
        /// <summary>
        /// 记录探针释放的事务数量。
        /// </summary>
        private long _transactionsDisposed;
        /// <summary>
        /// 记录探针当前打开的连接数量。
        /// </summary>
        private long _openConnections;
        /// <summary>
        /// 记录探针打开的连接数量峰值。
        /// </summary>
        private long _peakOpenConnections;

        /// <summary>
        /// 捕获当前资源计数快照。
        /// </summary>
        /// <returns>当前连接及事务计数的快照。</returns>
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

        /// <summary>
        /// 记录连接创建次数。
        /// </summary>
        public void RecordCreated() => Interlocked.Increment(ref _connectionsCreated);

        /// <summary>
        /// 记录连接打开次数及并发峰值。
        /// </summary>
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

        /// <summary>
        /// 记录连接关闭次数并减少当前打开数。
        /// </summary>
        public void RecordClosed()
        {
            Interlocked.Increment(ref _connectionsClosed);
            Interlocked.Decrement(ref _openConnections);
        }

        /// <summary>
        /// 记录连接释放并补记尚未关闭的连接。
        /// </summary>
        /// <param name="wasOpen">释放前连接是否仍处于打开状态。</param>
        public void RecordDisposed(bool wasOpen)
        {
            Interlocked.Increment(ref _connectionsDisposed);
            if (wasOpen)
                RecordClosed();
        }

        /// <summary>
        /// 记录事务开始次数。
        /// </summary>
        public void RecordTransactionBegin() => Interlocked.Increment(ref _transactionsBegun);

        /// <summary>
        /// 记录事务提交次数。
        /// </summary>
        public void RecordTransactionCommit() => Interlocked.Increment(ref _transactionsCommitted);

        /// <summary>
        /// 记录事务回滚次数。
        /// </summary>
        public void RecordTransactionRollback() => Interlocked.Increment(ref _transactionsRolledBack);

        /// <summary>
        /// 记录事务释放次数。
        /// </summary>
        public void RecordTransactionDisposed() => Interlocked.Increment(ref _transactionsDisposed);
    }

    /// <summary>
    /// 汇总工作单元基准探针的环境、阈值结果和计数。
    /// </summary>
    private sealed class ProbeReport
    {
        /// <summary>
        /// 获取或设置被测候选项名称。
        /// </summary>
        public string Candidate { get; set; }
        /// <summary>
        /// 获取或设置探针程序版本。
        /// </summary>
        public string HarnessVersion { get; set; }
        /// <summary>
        /// 获取或设置运行时版本标识。
        /// </summary>
        public string Runtime { get; set; }
        /// <summary>
        /// 获取或设置目标框架名称。
        /// </summary>
        public string TargetFramework { get; set; }
        /// <summary>
        /// 获取或设置状态值。
        /// </summary>
        public string Status { get; set; }
        /// <summary>
        /// 获取或设置测量允许的时间预算（秒）。
        /// </summary>
        public int TimeBudgetSeconds { get; set; }
        /// <summary>
        /// 获取或设置测量耗时（毫秒）。
        /// </summary>
        public double ElapsedMilliseconds { get; set; }
        /// <summary>
        /// 获取或设置请求使用的并发度。
        /// </summary>
        public int RequestedConcurrency { get; set; }
        /// <summary>
        /// 获取或设置实际观测到的最大并行度。
        /// </summary>
        public int MaximumActualParallelism { get; set; }
        /// <summary>
        /// 获取或设置观测到的最大排队请求数。
        /// </summary>
        public int MaximumQueuedRequests { get; set; }
        /// <summary>
        /// 获取或设置性能阈值检查状态。
        /// </summary>
        public string ThresholdStatus { get; set; }
        /// <summary>
        /// 获取或设置分配量的统计范围。
        /// </summary>
        public string AllocationScope { get; set; } = "process_gc_total";
        /// <summary>
        /// 获取或设置测量期间记录的失败信息。
        /// </summary>
        public string Failure { get; set; }
        /// <summary>
        /// 获取各项工作负载测量结果。
        /// </summary>
        public List<ProbeMeasurement> Measurements { get; } = new();
        /// <summary>
        /// 获取或设置探针结束时的计数快照。
        /// </summary>
        public ProbeCounterSnapshot FinalCounters { get; set; }
    }

    /// <summary>
    /// 记录单项工作单元基准的操作、耗时、分配和资源计数。
    /// </summary>
    private sealed class ProbeMeasurement
    {
        /// <summary>
        /// 获取或设置工作负载名称。
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 获取或设置预期执行的操作数。
        /// </summary>
        public int ExpectedOperations { get; set; }
        /// <summary>
        /// 获取或设置实际完成的操作数。
        /// </summary>
        public int CompletedOperations { get; set; }
        /// <summary>
        /// 获取或设置测量耗时（毫秒）。
        /// </summary>
        public double ElapsedMilliseconds { get; set; }
        /// <summary>
        /// 获取或设置测量期间分配的字节数。
        /// </summary>
        public long AllocatedBytes { get; set; }
        /// <summary>
        /// 获取或设置分配量的统计范围。
        /// </summary>
        public string AllocationScope { get; set; }
        /// <summary>
        /// 获取或设置创建的数据库连接数。
        /// </summary>
        public long ConnectionsCreated { get; set; }
        /// <summary>
        /// 获取或设置打开的数据库连接数。
        /// </summary>
        public long ConnectionsOpened { get; set; }
        /// <summary>
        /// 获取或设置关闭的数据库连接数。
        /// </summary>
        public long ConnectionsClosed { get; set; }
        /// <summary>
        /// 获取或设置释放的数据库连接数。
        /// </summary>
        public long ConnectionsDisposed { get; set; }
        /// <summary>
        /// 获取或设置开始的事务数。
        /// </summary>
        public long TransactionsBegun { get; set; }
        /// <summary>
        /// 获取或设置提交的事务数。
        /// </summary>
        public long TransactionsCommitted { get; set; }
        /// <summary>
        /// 获取或设置回滚的事务数。
        /// </summary>
        public long TransactionsRolledBack { get; set; }
        /// <summary>
        /// 获取或设置释放的事务数。
        /// </summary>
        public long TransactionsDisposed { get; set; }
        /// <summary>
        /// 获取或设置执行期间发生的异常数。
        /// </summary>
        public int Exceptions { get; set; }
        /// <summary>
        /// 获取或设置同时打开连接数的峰值。
        /// </summary>
        public long PeakOpenConnections { get; set; }
        /// <summary>
        /// 获取或设置请求使用的并发度。
        /// </summary>
        public int RequestedConcurrency { get; set; }
        /// <summary>
        /// 获取或设置实际观测到的最大并行度。
        /// </summary>
        public int MaximumActualParallelism { get; set; }
        /// <summary>
        /// 获取或设置观测到的最大排队请求数。
        /// </summary>
        public int MaximumQueuedRequests { get; set; }
    }

    /// <summary>
    /// 记录基准测量结束时的连接与事务计数。
    /// </summary>
    private sealed class ProbeCounterSnapshot
    {
        /// <summary>
        /// 获取或设置创建的数据库连接数。
        /// </summary>
        public long ConnectionsCreated { get; set; }
        /// <summary>
        /// 获取或设置打开的数据库连接数。
        /// </summary>
        public long ConnectionsOpened { get; set; }
        /// <summary>
        /// 获取或设置关闭的数据库连接数。
        /// </summary>
        public long ConnectionsClosed { get; set; }
        /// <summary>
        /// 获取或设置释放的数据库连接数。
        /// </summary>
        public long ConnectionsDisposed { get; set; }
        /// <summary>
        /// 获取或设置开始的事务数。
        /// </summary>
        public long TransactionsBegun { get; set; }
        /// <summary>
        /// 获取或设置提交的事务数。
        /// </summary>
        public long TransactionsCommitted { get; set; }
        /// <summary>
        /// 获取或设置回滚的事务数。
        /// </summary>
        public long TransactionsRolledBack { get; set; }
        /// <summary>
        /// 获取或设置释放的事务数。
        /// </summary>
        public long TransactionsDisposed { get; set; }
        /// <summary>
        /// 获取或设置当前仍打开的连接数。
        /// </summary>
        public long CurrentOpenConnections { get; set; }
        /// <summary>
        /// 获取或设置同时打开连接数的峰值。
        /// </summary>
        public long PeakOpenConnections { get; set; }
    }
}
