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
public abstract class SqliteDapperE2EBenchmarkBase
{
    private string _databasePath;
    private ServiceProvider _serviceProvider;
    private ServiceProvider _traceServiceProvider;
    private ISqlQueryFactory _queryFactory;
    private ISqlQueryFactory _traceQueryFactory;
    private ISqlMultipleQueryExecutor _multipleQueryExecutor;

    /// <summary>
    /// SQLite 样例行数。
    /// </summary>
    [Params(1, 100, 1000)]
    public int RowCount { get; set; }

    /// <summary>
    /// 创建独立临时数据库和查询服务。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), "bing-framework-benchmark-" + Guid.NewGuid().ToString("N") + ".db");
        var connectionString = $"Data Source={_databasePath};Mode=ReadWriteCreate;Pooling=False";
        _serviceProvider = CreateServiceProvider(connectionString, enableTrace: false);
        _traceServiceProvider = CreateServiceProvider(connectionString, enableTrace: true);
        _queryFactory = _serviceProvider.GetRequiredService<ISqlQueryFactory>();
        _traceQueryFactory = _traceServiceProvider.GetRequiredService<ISqlQueryFactory>();
        _multipleQueryExecutor = _serviceProvider.GetRequiredService<ISqlMultipleQueryExecutorFactory>().Create();
        SeedDatabase(connectionString);
        ValidateRepresentativePaths();
    }

    /// <summary>
    /// 测量 SQLite Dapper 查询、映射和列表物化的完整成本。
    /// </summary>
    [Benchmark]
    public int QueryToList()
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
    [Benchmark]
    public int QueryToEntity()
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
    [Benchmark]
    public int StreamToList()
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
    [Benchmark]
    public async Task<int> StreamAsyncToList()
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
    [Benchmark]
    public async Task<int> StreamAsyncCancelled()
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
    [Benchmark]
    public int MapTwoTypes()
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
    [Benchmark]
    public int MapFiveTypes()
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
    [Benchmark]
    public int MapSevenTypes()
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
    [Benchmark]
    public async Task<int> QueryMultiple()
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
    [Benchmark]
    public async Task<int> QueryMultipleDisposeEarly()
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
    [Benchmark]
    public int QueryToEntityCardinalityFailure()
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
    [Benchmark]
    public int QueryWithActivity()
    {
        using var activity = new Activity("bing-sql-benchmark").Start();
        return QueryToList();
    }

    /// <summary>
    /// 测量启用 DiagnosticListener 订阅时的查询诊断路径。
    /// </summary>
    [Benchmark]
    public int QueryWithDiagnosticListener()
    {
        using var observer = new NoOpDiagnosticObserver();
        return QueryToList();
    }

    /// <summary>
    /// 测量启用 Trace 日志时的查询路径。
    /// </summary>
    [Benchmark]
    public int QueryWithTrace()
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
    [GlobalCleanup]
    public void Cleanup()
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

    private void ValidateRepresentativePaths()
    {
        Ensure(QueryToList(), RowCount, nameof(QueryToList));
        Ensure(QueryToEntity(), 1, nameof(QueryToEntity));
        Ensure(StreamToList(), RowCount, nameof(StreamToList));
        Ensure(StreamAsyncToList().GetAwaiter().GetResult(), RowCount, nameof(StreamAsyncToList));
        Ensure(StreamAsyncCancelled().GetAwaiter().GetResult(), 1, nameof(StreamAsyncCancelled));
        Ensure(MapTwoTypes(), RowCount, nameof(MapTwoTypes));
        Ensure(MapFiveTypes(), RowCount, nameof(MapFiveTypes));
        Ensure(MapSevenTypes(), RowCount, nameof(MapSevenTypes));
        Ensure(QueryMultiple().GetAwaiter().GetResult(), RowCount * 2, nameof(QueryMultiple));
        Ensure(QueryMultipleDisposeEarly().GetAwaiter().GetResult(), RowCount, nameof(QueryMultipleDisposeEarly));
        Ensure(QueryToEntityCardinalityFailure(), 1, nameof(QueryToEntityCardinalityFailure));
        Ensure(QueryWithActivity(), RowCount, nameof(QueryWithActivity));
        Ensure(QueryWithDiagnosticListener(), RowCount, nameof(QueryWithDiagnosticListener));
        Ensure(QueryWithTrace(), RowCount, nameof(QueryWithTrace));
    }

    private static void Ensure(int actual, int expected, string path)
    {
        if (actual != expected)
            throw new InvalidOperationException($"SQLite E2E validation failed for {path}: expected {expected}, actual {actual}.");
    }

    private void SeedDatabase(string connectionString)
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
        for (var index = 1; index <= RowCount; index++)
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

    private sealed class NoOpDiagnosticObserver : IObserver<DiagnosticListener>,
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

[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqliteDapperE2EBenchmarks : SqliteDapperE2EBenchmarkBase
{
}

[MemoryDiagnoser]
[DryJob]
public class SqliteDapperE2ESmokeBenchmarks : SqliteDapperE2EBenchmarkBase
{
}