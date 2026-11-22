using BenchmarkDotNet.Attributes;
using System;
using System.IO;
using System.Linq;
using Bing.Data.Enums;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Bing.Uow;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>在相同 SQL、数据量及事务语义下比较兼容与生成路径，包含真实连接及执行成本。</summary>
[MemoryDiagnoser]
public class SqlGeneratedSqliteBenchmarks
{
    private string _path;
    private ServiceProvider _provider;
    private IServiceScope _services;
    private ISqlQueryFactory _queries;
    private ISqlTransactionScopeFactory _transactions;
    private IUnitOfWorkScopeManager _uow;
    private readonly Filter _filter = new() { Minimum = 1 };

    [Params(SqlExecutionMode.Compatible, SqlExecutionMode.GeneratedOnly)]
    public SqlExecutionMode Mode { get; set; }

    [Params(1, 32, 256)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), "bing-generated-bench-" + Guid.NewGuid().ToString("N") + ".db");
        var connectionString = "Data Source=" + _path;
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE rows(Id INTEGER PRIMARY KEY, Name TEXT);" +
                string.Join("", Enumerable.Range(1, Rows).Select(id => $"INSERT INTO rows VALUES({id},'name-{id}');"));
            command.ExecuteNonQuery();
        }
        var services = new ServiceCollection();
        services.AddUnitOfWorkScopes();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = Mode);
        services.AddSqlGeneratedModels(new[]
        {
            new EntityModelMetadata(typeof(Row), "rows", null, new[]
            {
                EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true,
                    generatedGetter: value => ((Row)value).Id, generatedSetter: (value, id) => ((Row)value).Id = (int)id)
                    .WithGeneratedReader(static (value, reader, ordinal) =>
                    {
                        if (reader.GetFieldType(ordinal) != typeof(long)) return false;
                        ((Row)value).Id = checked((int)reader.GetInt64(ordinal)); return true;
                    }),
                EntityPropertyMetadata.CreateGenerated(nameof(Row.Name), typeof(string), true, true,
                    generatedGetter: value => ((Row)value).Name, generatedSetter: (value, name) => ((Row)value).Name = (string)name)
                    .WithGeneratedReader(static (value, reader, ordinal) =>
                    {
                        if (reader.GetFieldType(ordinal) != typeof(string)) return false;
                        ((Row)value).Name = reader.GetString(ordinal); return true;
                    })
            }, () => new Row()),
            new EntityModelMetadata(typeof(Filter), "filter", null, new[]
            {
                EntityPropertyMetadata.CreateGenerated(nameof(Filter.Minimum), typeof(int), true, true,
                    generatedGetter: value => ((Filter)value).Minimum)
            })
        });
        services.AddSqlDataSource("default", DatabaseType.Sqlite, connectionString);
        services.AddSqliteProvider();
        _provider = services.BuildServiceProvider();
        _services = _provider.CreateScope();
        _queries = _services.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
        _transactions = _services.ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>();
        _uow = _services.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        if (NoTransaction() != Rows || SharedTransaction() != Rows || NonTransactionalUow() != Rows)
            throw new InvalidOperationException("E2E workload did not return the expected rows");
    }

    [Benchmark]
    public int NoTransaction()
    {
        using var query = _queries.Create();
        return Read(query);
    }

    [Benchmark]
    public int SharedTransaction()
    {
        using var transaction = _transactions.Begin();
        using var writer = transaction.CreateExecutor();
        using var query = transaction.CreateQuery();
        writer.ExecuteSql("UPDATE rows SET Name=Name WHERE Id=1");
        var result = Read(query);
        transaction.Commit();
        return result;
    }

    [Benchmark]
    public int NonTransactionalUow()
    {
        using var uow = _uow.Begin(new UnitOfWorkScopeOptions { IsTransactional = false });
        using var query = _queries.Create();
        var result = Read(query);
        uow.Complete();
        return result;
    }

    private int Read(ISqlQuery query) => query.Sql("SELECT Id, Name FROM rows WHERE Id>=@Minimum ORDER BY Id", _filter).ToList<Row>().Count;

    /// <summary>保留旧公开包装合同，作为相同候选中逐行包装成本的对照。</summary>
    [Benchmark]
    public int LambdaWrapped()
    {
        using var query = _queries.Create();
        return SqlGeneratedExecution.ToList(query.From<Row>(), row => new Row
        {
            Id = row.TryGetOrdinal("Id", out var id) ? row.Reader.GetInt32(id) : 0,
            Name = row.TryGetOrdinal("Name", out var name) ? row.Reader.GetString(name) : null
        }, static (_, _) => { }, null).Count;
    }

    /// <summary>模拟生成代码的绑定工厂，每个结果集固定序号，逐行直接读取。</summary>
    [Benchmark]
    public int LambdaBound()
    {
        using var query = _queries.Create();
        return SqlGeneratedExecution.ToBoundList<Row>(query.From<Row>(),
            static ordinals =>
            {
                var id = ordinals["Id"]; var name = ordinals["Name"];
                return reader => new Row { Id = reader.GetInt32(id), Name = reader.GetString(name) };
            }, static (_, _) => { }, null).Count;
    }

    [Benchmark]
    public int FirstRow()
    {
        using var query = _queries.Create();
        return query.Sql("SELECT Id, Name FROM rows ORDER BY Id").First<Row>().Id;
    }

    [Benchmark]
    public int SingleRow()
    {
        using var query = _queries.Create();
        return query.Sql("SELECT Id, Name FROM rows WHERE Id=1").Single<Row>().Id;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _services?.Dispose();
        _provider?.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    public sealed class Row { public int Id { get; set; } public string Name { get; set; } }
    public sealed class Filter { public int Minimum { get; set; } }
}
