using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Diagnostics;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// 使用临时 SQLite 文件验证 Dapper Query/ToList 真实执行成本的端到端基准。
/// </summary>
public abstract class SqliteDapperE2EBenchmarkInfrastructure
{
    private string _databasePath;
    private ServiceProvider _serviceProvider;
    private ServiceProvider _traceServiceProvider;
    private ISqlQueryFactory _queryFactory;
    private ISqlQueryFactory _traceQueryFactory;
    private ISqlMultipleQueryExecutor _multipleQueryExecutor;

    /// <summary>
    /// 创建独立临时数据库和查询服务。
    /// </summary>
    /// <param name="rowCount">需要写入 SQLite 样例表的行数。</param>
    /// <param name="enableTrace">是否为 Trace 场景额外创建日志服务。</param>
    protected void Initialize(int rowCount, bool enableTrace = false)
    {
        _databasePath = Path.Combine(Path.GetTempPath(), "bing-framework-benchmark-" + Guid.NewGuid().ToString("N") + ".db");
        var connectionString = $"Data Source={_databasePath};Mode=ReadWriteCreate;Pooling=False";
        _serviceProvider = CreateServiceProvider(connectionString, enableTrace: false);
        _traceServiceProvider = enableTrace ? CreateServiceProvider(connectionString, enableTrace: true) : null;
        _queryFactory = _serviceProvider.GetRequiredService<ISqlQueryFactory>();
        _traceQueryFactory = _traceServiceProvider?.GetRequiredService<ISqlQueryFactory>();
        _multipleQueryExecutor = _serviceProvider.GetRequiredService<ISqlMultipleQueryExecutorFactory>().Create();
        SeedDatabase(connectionString, rowCount);
    }

    /// <summary>
    /// 测量 SQLite Dapper 查询、映射和列表物化的完整成本。
    /// </summary>
    protected int ExecuteQueryToList()
    {
        using var query = CreateQuery(_queryFactory);
        return query.Query()
            .Select("Id,Name")
            .From("samples")
            .OrderBy("Id")
            .ToList<SqliteBenchmarkRow>()
            .Count;
    }

    /// <summary>
    /// 测量 SQLite Dapper 单实体终结路径。
    /// </summary>
    protected int ExecuteQueryToEntity()
    {
        using var query = CreateQuery(_queryFactory);
        return query.Query()
            .Select("Id,Name")
            .From("samples")
            .Where("Id", 1)
            .OrderBy("Id")
            .ToEntity<SqliteBenchmarkRow>()
            .Id;
    }

    /// <summary>
    /// 测量 SQLite Dapper 同步流式读取和释放路径。
    /// </summary>
    protected int ExecuteStreamToList()
    {
        using var query = CreateQuery(_queryFactory);
        using var enumerator = query.Query()
            .Select("Id,Name")
            .From("samples")
            .OrderBy("Id")
            .AsEnumerable<SqliteBenchmarkRow>()
            .GetEnumerator();
        var count = 0;
        while (enumerator.MoveNext())
            count++;
        return count;
    }

    /// <summary>
    /// 测量 SQLite Dapper 异步流式读取路径。
    /// </summary>
    protected async Task<int> ExecuteStreamAsyncToList()
    {
        using var query = CreateQuery(_queryFactory);
        var count = 0;
        await foreach (var _ in query.Query()
                           .Select("Id,Name")
                           .From("samples")
                           .OrderBy("Id")
                           .AsAsyncEnumerable<SqliteBenchmarkRow>())
            count++;
        return count;
    }

    /// <summary>
    /// 测量异步流在取消后释放读取器的路径。
    /// </summary>
    protected async Task<int> ExecuteStreamAsyncCancelled()
    {
        using var query = CreateQuery(_queryFactory);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        try
        {
            await foreach (var _ in query.Query()
                               .Select("Id,Name")
                               .From("samples")
                               .AsAsyncEnumerable<SqliteBenchmarkRow>(cancellationToken: cancellationTokenSource.Token))
            {
            }
        }
        catch (OperationCanceledException)
        {
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// 测量 SQLite Dapper 双对象映射路径。
    /// </summary>
    protected int ExecuteMapTwoTypes()
    {
        using var query = CreateQuery(_queryFactory);
        return query.Query()
            .Select("Id,Name,Id,Name")
            .From("samples")
            .OrderBy("Id")
            .ToList<SqliteBenchmarkRow, SqliteBenchmarkName, SqliteBenchmarkPair>((row, name) => new SqliteBenchmarkPair
            {
                Id = row.Id,
                Name = name.Name
            })
            .Count;
    }

    /// <summary>
    /// 测量 SQLite Dapper 五对象映射路径。
    /// </summary>
    protected int ExecuteMapFiveTypes()
    {
        using var query = CreateQuery(_queryFactory);
        return query.Query()
            .Select("Id,Name,Id,Name,Id,Name,Id,Name,Id,Name")
            .From("samples")
            .OrderBy("Id")
            .ToList<SqliteBenchmarkRow, SqliteBenchmarkName, SqliteBenchmarkName, SqliteBenchmarkName,
                SqliteBenchmarkName, SqliteBenchmarkFive>((row, second, third, fourth, fifth) => new SqliteBenchmarkFive
            {
                Id = row.Id,
                Names = string.Join(":", second.Name, third.Name, fourth.Name, fifth.Name)
            })
            .Count;
    }

    /// <summary>
    /// 测量 SQLite Dapper 七对象映射路径。
    /// </summary>
    protected int ExecuteMapSevenTypes()
    {
        using var query = CreateQuery(_queryFactory);
        return query.Query()
            .Select("Id,Name,Id,Name,Id,Name,Id,Name,Id,Name,Id,Name,Id,Name")
            .From("samples")
            .OrderBy("Id")
            .ToList<SqliteBenchmarkRow, SqliteBenchmarkName, SqliteBenchmarkName, SqliteBenchmarkName,
                SqliteBenchmarkName, SqliteBenchmarkName, SqliteBenchmarkName, SqliteBenchmarkSeven>(
                (row, second, third, fourth, fifth, sixth, seventh) => new SqliteBenchmarkSeven
                {
                    Id = row.Id,
                    Names = string.Join(":", second.Name, third.Name, fourth.Name, fifth.Name, sixth.Name, seventh.Name)
                })
            .Count;
    }

    /// <summary>
    /// 测量 SQLite Dapper 多结果集读取和结果对象释放路径。
    /// </summary>
    protected async Task<int> ExecuteQueryMultiple()
    {
        var command = _multipleQueryExecutor.CreateBatch()
            .Append("Select Id,Name From samples Order By Id")
            .Append("Select Count(*) From samples")
            .Build();
        await using var result = await _multipleQueryExecutor.ExecuteAsync(command);
        var rows = await result.ReadAsync<SqliteBenchmarkRow>(CancellationToken.None);
        var count = (await result.ReadAsync<int>(CancellationToken.None)).Single();
        return rows.Count + count;
    }

    /// <summary>
    /// 测量多结果集只读取首个结果后提前释放的路径。
    /// </summary>
    protected async Task<int> ExecuteQueryMultipleDisposeEarly()
    {
        var command = _multipleQueryExecutor.CreateBatch()
            .Append("Select Id,Name From samples Order By Id")
            .Append("Select Count(*) From samples")
            .Build();
        await using var result = await _multipleQueryExecutor.ExecuteAsync(command);
        return (await result.ReadAsync<SqliteBenchmarkRow>(CancellationToken.None)).Count;
    }

    /// <summary>
    /// 测量单实体基数异常路径，并确认异常可被调用方捕获。
    /// </summary>
    protected int ExecuteQueryToEntityCardinalityFailure()
    {
        using var query = CreateQuery(_queryFactory);
        try
        {
            query.Sql("Select Id,Name From samples Union All Select Id,Name From samples")
                .ToEntity<SqliteBenchmarkRow>();
        }
        catch (InvalidOperationException)
        {
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// 测量仅启用 Activity 时的查询诊断路径。
    /// </summary>
    protected int ExecuteQueryWithActivity()
    {
        using var activity = new Activity("bing-sql-benchmark").Start();
        return ExecuteQueryToList();
    }

    /// <summary>
    /// 测量启用 Trace 日志时的查询路径。
    /// </summary>
    protected int ExecuteQueryWithTrace()
    {
        using var query = CreateQuery(_traceQueryFactory);
        return query.Query()
            .Select("Id,Name")
            .From("samples")
            .OrderBy("Id")
            .ToList<SqliteBenchmarkRow>()
            .Count;
    }

    /// <summary>
    /// 释放服务和临时数据库文件。
    /// </summary>
    protected void CleanupResources()
    {
        _multipleQueryExecutor?.Dispose();
        _multipleQueryExecutor = null;
        _serviceProvider?.Dispose();
        _serviceProvider = null;
        _traceServiceProvider?.Dispose();
        _traceServiceProvider = null;
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    private static ServiceProvider CreateServiceProvider(string connectionString, bool enableTrace)
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSqliteProvider();
        services.AddSqlDataSource("default", DatabaseType.Sqlite, connectionString);
        if (enableTrace)
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddProvider(new TraceEnabledNoOpLoggerProvider());
            });
        return services.BuildServiceProvider();
    }

    private static ISqlQuery CreateQuery(ISqlQueryFactory queryFactory) => queryFactory.Create();

    /// <summary>
    /// 验证初始化时执行的代表路径结果。
    /// </summary>
    /// <param name="actual">实际结果。</param>
    /// <param name="expected">预期结果。</param>
    /// <param name="path">验证路径名称。</param>
    protected static void Ensure(int actual, int expected, string path)
    {
        if (actual != expected)
            throw new InvalidOperationException($"SQLite E2E validation failed for {path}: expected {expected}, actual {actual}.");
    }

    private void SeedDatabase(string connectionString, int rowCount)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "Create Table samples(Id Integer Primary Key, Name Text Not Null);";
        command.ExecuteNonQuery();
        using var transaction = connection.BeginTransaction();
        command.Transaction = transaction;
        command.CommandText = "Insert Into samples(Id, Name) Values (@id, @name);";
        var idParameter = command.CreateParameter();
        idParameter.ParameterName = "@id";
        command.Parameters.Add(idParameter);
        var nameParameter = command.CreateParameter();
        nameParameter.ParameterName = "@name";
        command.Parameters.Add(nameParameter);
        for (var index = 1; index <= rowCount; index++)
        {
            idParameter.Value = index;
            nameParameter.Value = $"row-{index}";
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private sealed class SqliteBenchmarkRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    private sealed class SqliteBenchmarkName
    {
        public string Name { get; set; }
    }

    private sealed class SqliteBenchmarkPair
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    private sealed class SqliteBenchmarkFive
    {
        public int Id { get; set; }
        public string Names { get; set; }
    }

    private sealed class SqliteBenchmarkSeven
    {
        public int Id { get; set; }
        public string Names { get; set; }
    }

    protected sealed class NoOpDiagnosticObserver : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object>>, IDisposable
    {
        private readonly IDisposable _allListenersSubscription;
        private IDisposable _listenerSubscription;

        public NoOpDiagnosticObserver() => _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == SqlQueryDiagnosticListenerNames.DiagnosticListenerName)
                _listenerSubscription = value.Subscribe(this);
        }

        public void OnNext(KeyValuePair<string, object> value) { }
        public void OnCompleted() { }
        public void OnError(Exception error) { }

        public void Dispose()
        {
            _listenerSubscription?.Dispose();
            _allListenersSubscription.Dispose();
        }
    }

    private sealed class TraceEnabledNoOpLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => TraceEnabledNoOpLogger.Instance;

        public void Dispose() { }
    }

    private sealed class TraceEnabledNoOpLogger : ILogger
    {
        public static TraceEnabledNoOpLogger Instance { get; } = new();

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Trace;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}

/// <summary>
/// 行数会影响结果的 SQLite Dapper E2E 基准基类。
/// </summary>
public abstract class SqliteDapperE2EScalableBenchmarksBase : SqliteDapperE2EBenchmarkInfrastructure
{
    /// <summary>
    /// SQLite 样例行数。
    /// </summary>
    [Params(1, 100, 1000)]
    public int RowCount { get; set; }

    /// <summary>
    /// 创建按行数扩展的 SQLite 测量环境。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        Initialize(RowCount);
        Ensure(ExecuteQueryToList(), RowCount, nameof(QueryToList));
        Ensure(ExecuteStreamToList(), RowCount, nameof(StreamToList));
        Ensure(ExecuteStreamAsyncToList().GetAwaiter().GetResult(), RowCount, nameof(StreamAsyncToList));
        Ensure(ExecuteMapTwoTypes(), RowCount, nameof(MapTwoTypes));
        Ensure(ExecuteMapFiveTypes(), RowCount, nameof(MapFiveTypes));
        Ensure(ExecuteMapSevenTypes(), RowCount, nameof(MapSevenTypes));
        Ensure(ExecuteQueryMultiple().GetAwaiter().GetResult(), RowCount * 2, nameof(QueryMultiple));
        Ensure(ExecuteQueryMultipleDisposeEarly().GetAwaiter().GetResult(), RowCount, nameof(QueryMultipleDisposeEarly));
    }

    /// <summary>
    /// 释放按行数扩展的 SQLite 测量环境。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => CleanupResources();

    /// <summary>
    /// 测量 SQLite Dapper 查询、映射和列表物化的完整成本。
    /// </summary>
    [Benchmark]
    public int QueryToList() => ExecuteQueryToList();

    /// <summary>
    /// 测量 SQLite Dapper 同步流式读取和释放路径。
    /// </summary>
    [Benchmark]
    public int StreamToList() => ExecuteStreamToList();

    /// <summary>
    /// 测量 SQLite Dapper 异步流式读取路径。
    /// </summary>
    [Benchmark]
    public Task<int> StreamAsyncToList() => ExecuteStreamAsyncToList();

    /// <summary>
    /// 测量 SQLite Dapper 双对象映射路径。
    /// </summary>
    [Benchmark]
    public int MapTwoTypes() => ExecuteMapTwoTypes();

    /// <summary>
    /// 测量 SQLite Dapper 五对象映射路径。
    /// </summary>
    [Benchmark]
    public int MapFiveTypes() => ExecuteMapFiveTypes();

    /// <summary>
    /// 测量 SQLite Dapper 七对象映射路径。
    /// </summary>
    [Benchmark]
    public int MapSevenTypes() => ExecuteMapSevenTypes();

    /// <summary>
    /// 测量 SQLite Dapper 多结果集读取和结果对象释放路径。
    /// </summary>
    [Benchmark]
    public Task<int> QueryMultiple() => ExecuteQueryMultiple();

    /// <summary>
    /// 测量多结果集只读取首个结果后提前释放的路径。
    /// </summary>
    [Benchmark]
    public Task<int> QueryMultipleDisposeEarly() => ExecuteQueryMultipleDisposeEarly();
}

/// <summary>
/// 默认 listener-off 的按行数 SQLite Dapper E2E 基准。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqliteDapperE2EBenchmarks : SqliteDapperE2EScalableBenchmarksBase
{
}

/// <summary>
/// 固定输入的 SQLite Dapper E2E 基准基础设施。
/// </summary>
public abstract class SqliteDapperE2EFixedBenchmarksBase : SqliteDapperE2EBenchmarkInfrastructure
{
    /// <summary>
    /// 创建固定单行 SQLite 测量环境。
    /// </summary>
    /// <param name="enableTrace">是否创建 Trace 场景需要的日志服务。</param>
    protected void SetupFixed(bool enableTrace = false) => Initialize(1, enableTrace);

    /// <summary>
    /// 释放固定输入 SQLite 测量环境。
    /// </summary>
    protected void CleanupFixed() => CleanupResources();
}

/// <summary>
/// 固定输入的 SQLite Dapper 单实体终结基准。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqliteDapperE2ETerminalBenchmarks : SqliteDapperE2EFixedBenchmarksBase
{
    /// <summary>
    /// 创建固定单实体终结测量环境。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        SetupFixed();
        Ensure(ExecuteQueryToEntity(), 1, nameof(QueryToEntity));
    }

    /// <summary>
    /// 释放单实体终结测量环境。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => CleanupFixed();

    /// <summary>
    /// 测量 SQLite Dapper 单实体终结路径。
    /// </summary>
    [Benchmark]
    public int QueryToEntity() => ExecuteQueryToEntity();
}

/// <summary>
/// 预取消异步流的 SQLite Dapper 基准。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqliteDapperE2ECancellationBenchmarks : SqliteDapperE2EFixedBenchmarksBase
{
    /// <summary>
    /// 创建预取消异步流测量环境。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        SetupFixed();
        Ensure(ExecuteStreamAsyncCancelled().GetAwaiter().GetResult(), 1, nameof(StreamAsyncCancelled));
    }

    /// <summary>
    /// 释放预取消异步流测量环境。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => CleanupFixed();

    /// <summary>
    /// 测量异步流在取消后释放读取器的路径。
    /// </summary>
    [Benchmark]
    public Task<int> StreamAsyncCancelled() => ExecuteStreamAsyncCancelled();
}

/// <summary>
/// 单实体基数异常的 SQLite Dapper 基准。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqliteDapperE2ECardinalityBenchmarks : SqliteDapperE2EFixedBenchmarksBase
{
    /// <summary>
    /// 创建基数异常测量环境。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        SetupFixed();
        Ensure(ExecuteQueryToEntityCardinalityFailure(), 1, nameof(QueryToEntityCardinalityFailure));
    }

    /// <summary>
    /// 释放基数异常测量环境。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => CleanupFixed();

    /// <summary>
    /// 测量单实体基数异常路径，并确认异常可被调用方捕获。
    /// </summary>
    [Benchmark]
    public int QueryToEntityCardinalityFailure() => ExecuteQueryToEntityCardinalityFailure();
}

/// <summary>
/// 仅启用 Activity 的固定输入 SQLite Dapper 查询基准。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqliteDapperE2EActivityBenchmarks : SqliteDapperE2EFixedBenchmarksBase
{
    /// <summary>
    /// 创建 Activity 测量环境。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        SetupFixed();
        Ensure(ExecuteQueryWithActivity(), 1, nameof(QueryWithActivity));
    }

    /// <summary>
    /// 释放 Activity 测量环境。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => CleanupFixed();

    /// <summary>
    /// 测量仅启用 Activity 时的查询诊断路径。
    /// </summary>
    [Benchmark]
    public int QueryWithActivity() => ExecuteQueryWithActivity();
}

/// <summary>
/// 启用 Trace 日志的固定输入 SQLite Dapper 查询基准。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqliteDapperE2ETraceBenchmarks : SqliteDapperE2EFixedBenchmarksBase
{
    /// <summary>
    /// 创建 Trace 测量环境。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        SetupFixed(enableTrace: true);
        Ensure(ExecuteQueryWithTrace(), 1, nameof(QueryWithTrace));
    }

    /// <summary>
    /// 释放 Trace 测量环境。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => CleanupFixed();

    /// <summary>
    /// 测量启用 Trace 日志时的查询路径。
    /// </summary>
    [Benchmark]
    public int QueryWithTrace() => ExecuteQueryWithTrace();
}

/// <summary>
/// 已订阅 DiagnosticListener 时的稳态 SQLite Dapper 查询基准。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqliteDapperE2EDiagnosticSteadyBenchmarks : SqliteDapperE2EFixedBenchmarksBase
{
    private IDisposable _observer;

    /// <summary>
    /// 创建整个 benchmark 生命周期保持的诊断订阅。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        SetupFixed();
        _observer = new NoOpDiagnosticObserver();
        Ensure(ExecuteQueryToList(), 1, nameof(QueryWithDiagnosticListener));
    }

    /// <summary>
    /// 释放稳态诊断订阅和 SQLite 测量环境。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _observer?.Dispose();
        _observer = null;
        CleanupResources();
    }

    /// <summary>
    /// 测量已订阅 DiagnosticListener 时的稳态查询路径。
    /// </summary>
    [Benchmark]
    public int QueryWithDiagnosticListener() => ExecuteQueryToList();
}

/// <summary>
/// 建立 DiagnosticListener 订阅并执行一次 SQLite Dapper 查询的基准。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqliteDapperE2EDiagnosticSubscribeBenchmarks : SqliteDapperE2EFixedBenchmarksBase
{
    /// <summary>
    /// 创建订阅成本测量环境。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        SetupFixed();
        Ensure(SubscribeDiagnosticListenerAndQuery(), 1, nameof(SubscribeDiagnosticListenerAndQuery));
    }

    /// <summary>
    /// 释放订阅成本测量环境。
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => CleanupFixed();

    /// <summary>
    /// 测量建立订阅后执行一次查询的成本。
    /// </summary>
    [Benchmark]
    public int SubscribeDiagnosticListenerAndQuery()
    {
        using var observer = new NoOpDiagnosticObserver();
        return ExecuteQueryToList();
    }
}

[MemoryDiagnoser]
[DryJob]
public class SqliteDapperE2ESmokeBenchmarks : SqliteDapperE2EScalableBenchmarksBase
{
}