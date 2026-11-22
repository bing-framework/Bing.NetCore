using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>相同生成 Getter、绑定器和映射下，对比逐次渲染与完整生成 SQL 的命令构建分配。</summary>
[MemoryDiagnoser]
public class SqlGeneratedMutationBenchmarks
{
    private const string InsertSql = "Insert Into `generated_mutation_rows` (`Id`, `Name`, `Version`) Values (@_p_0, @_p_1, @_p_2)";
    private const string UpdateSql = "Update `generated_mutation_rows` Set `Name` = @_p_0, `Version` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3";
    private const string DeleteSql = "Delete From `generated_mutation_rows` Where `Id` = @_p_0 And `Version` = @_p_1";
    private readonly Row _row = new() { Id = 7, Name = "sample", Version = 2 };
    private DefaultSqlEntityMutationCommandBuilder _builder;
    private ISqlProvider _provider;
    private string _insertSql;
    private string _updateSql;
    private string _deleteSql;
    private string _singleInsertSql;
    private string _filteredUpdateSql;
    private string _doubleInsertSql;
    private Row[] _rows;
    private readonly SqlInsertOptions _singleInsert = new() { IncludeProperties = new[] { "Name" } };
    private readonly SqlUpdateOptions _filteredUpdate = new() { IncludeProperties = new[] { "Name" } };

    /// <summary>每个 Provider 分别比较自己的渲染与常量路径，不将跨 Provider 结果当作前后对比。</summary>
    [Params("SQLite", "MySQL", "PostgreSQL", "SqlServer")]
    public string Provider { get; set; } = "SQLite";

    /// <summary>是否登记完整生成 SQL；其余配置及工作负载保持一致。</summary>
    [Params(false, true)]
    public bool UseGeneratedSql { get; set; }

    /// <summary>测量的单实体操作。</summary>
    [Params("Insert", "Update", "Delete", "SingleInsert", "FilteredUpdate", "DoubleInsert")]
    public string Operation { get; set; }

    /// <summary>创建独立映射缓存并预热，检查 SQL 与静态绑定器相同。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _provider = Provider switch
        {
            "SQLite" => SqliteSqlProvider.Instance,
            "MySQL" => MySqlSqlProvider.Instance,
            "PostgreSQL" => PostgreSqlSqlProvider.Instance,
            "SqlServer" => SqlServerSqlProvider.Instance,
            _ => throw new InvalidOperationException("Unknown provider.")
        };
        var open = _provider.Dialect.OpeningIdentifier;
        var close = _provider.Dialect.ClosingIdentifier;
        string Quote(string sql)
        {
            var opening = true;
            var text = new StringBuilder(sql.Length);
            foreach (var character in sql)
                if (character == '`') { text.Append(opening ? open : close); opening = !opening; }
                else text.Append(character);
            return text.ToString();
        }
        _insertSql = Quote(InsertSql);
        _updateSql = Quote(UpdateSql);
        _deleteSql = Quote(DeleteSql);
        _singleInsertSql = Quote("Insert Into `generated_mutation_rows` (`Name`) Values (@_p_0)");
        _filteredUpdateSql = Quote("Update `generated_mutation_rows` Set `Name` = @_p_0 Where `Id` = @_p_1 And `Version` = @_p_2");
        _doubleInsertSql = Quote(InsertSql + ", (@_p_3, @_p_4, @_p_5)");
        _rows = new[] { _row, new Row { Id = 8, Name = "second", Version = 3 } };
        var model = new EntityModelMetadata(typeof(Row), "generated_mutation_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true, isKey: true,
                generatedGetter: source => ((Row)source).Id),
            EntityPropertyMetadata.CreateGenerated("Name", typeof(string), true, true,
                generatedGetter: source => ((Row)source).Name),
            EntityPropertyMetadata.CreateGenerated("Version", typeof(int), true, true, isConcurrencyToken: true,
                generatedGetter: source => ((Row)source).Version)
        }, () => new Row())
        {
            GeneratedMutationSql = UseGeneratedSql ? GetSql : null,
            GeneratedMutationShapeSql = UseGeneratedSql ? GetShapeSql : null
        };
        _builder = new DefaultSqlEntityMutationCommandBuilder(_provider, new SqlBuilderServices(
            metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model)));
        var command = BuildCommand();
        var expected = Operation switch
        {
            "SingleInsert" => _singleInsertSql,
            "FilteredUpdate" => _filteredUpdateSql,
            "DoubleInsert" => _doubleInsertSql,
            _ => GetSql(_provider.Key, command.OperationKind)
        };
        if (command.Sql != expected || !command.IsGeneratedExecution ||
            UseGeneratedSql && !ReferenceEquals(command.Sql, expected))
            throw new InvalidOperationException("Generated SQL benchmark did not reach the expected production path.");
    }

    /// <summary>构造完整命令和参数快照；不包含数据库 IO。</summary>
    [Benchmark]
    public SqlWriteCommand BuildCommand() => Operation switch
    {
        "Insert" => _builder.Insert(_row),
        "Update" => _builder.Update(_row),
        "Delete" => _builder.Purge(_row),
        "SingleInsert" => _builder.Insert(_row, _singleInsert),
        "FilteredUpdate" => _builder.Update(_row, _filteredUpdate),
        "DoubleInsert" => _builder.InsertCombined(_rows),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    private string GetSql(string provider, SqlOperationKind operation) => provider != _provider.Key ? null : operation switch
    {
        SqlOperationKind.InsertValues => _insertSql,
        SqlOperationKind.Update => _updateSql,
        SqlOperationKind.Delete => _deleteSql,
        _ => null
    };

    private string GetShapeSql(string provider, SqlOperationKind operation, string property, int rows) =>
        provider != _provider.Key ? null : (operation, property, rows) switch
        {
            (SqlOperationKind.InsertValues, "Name", 1) => _singleInsertSql,
            (SqlOperationKind.Update, "Name", 1) => _filteredUpdateSql,
            (SqlOperationKind.InsertValues, null, 2) => _doubleInsertSql,
            _ => null
        };

    /// <summary>有界本机分配诊断，同一候选切换 SQL 来源；不作为历史基线或生产性能门禁。</summary>
    internal static int RunAllocationProbe(string output)
    {
        const int iterations = 10000;
        var results = new System.Collections.Generic.List<object>();
        foreach (var provider in new[] { "SQLite", "MySQL", "PostgreSQL", "SqlServer" })
        foreach (var operation in new[] { "Insert", "Update", "Delete", "SingleInsert", "FilteredUpdate", "DoubleInsert" })
        foreach (var generated in new[] { false, true })
        {
            var benchmark = new SqlGeneratedMutationBenchmarks { Provider = provider, Operation = operation, UseGeneratedSql = generated };
            benchmark.Setup();
            for (var i = 0; i < 2048; i++) benchmark.BuildCommand();
            var samples = new double[5];
            SqlWriteCommand last = null;
            for (var sample = 0; sample < samples.Length; sample++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < iterations; i++) last = benchmark.BuildCommand();
                samples[sample] = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)iterations;
            }
            GC.KeepAlive(last);
            results.Add(new { provider, operation, generatedSql = generated, allocatedBytesPerOperation = samples });
        }
        var report = new { probe = "GeneratedMutationSql-v3", runtime = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), warmup = 2048, iterations, results };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
        Console.WriteLine("GENERATED_MUTATION_ALLOCATION_PROBE_PASS");
        return 0;
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
    }
}
