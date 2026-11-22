using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>同一候选、模型及绑定器下，对比身份键插入的生成文本与运行时渲染分配。</summary>
[MemoryDiagnoser]
public class SqlGeneratedKeyInsertBenchmarks
{
    private const string ValuesSql = "Insert Into `key_rows` (`Name`) Values (@_p_0) Returning `Id`";
    private const string EmptySql = "Insert Into `key_rows` DEFAULT VALUES RETURNING `Id`";
    private readonly Row _row = new() { Name = "sample" };
    private SqlBuilderServices _services;
    private EntityKeyPolicy _policy;

    [Params(false, true)] public bool UseGeneratedSql { get; set; }
    [Params(false, true)] public bool DefaultValues { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var properties = new List<EntityPropertyMetadata>
        {
            EntityPropertyMetadata.CreateGenerated("Id", typeof(long), true, true, isKey: true,
                databaseGeneratedOption: DatabaseGeneratedOption.Identity, generatedGetter: source => ((Row)source).Id)
        };
        if (!DefaultValues)
            properties.Add(EntityPropertyMetadata.CreateGenerated("Name", typeof(string), true, true,
                generatedGetter: source => ((Row)source).Name));
        var sql = DefaultValues ? EmptySql : ValuesSql;
        var model = new EntityModelMetadata(typeof(Row), "key_rows", null, properties, () => new Row())
        {
            GeneratedKeyInsertSql = UseGeneratedSql ? provider => provider == "bing.sqlite" ? sql : null : null
        };
        _services = new SqlBuilderServices(metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model));
        _policy = EntityKeyPolicy.CreateGenerated<Row, long>("Id", EntityKeyGenerationKind.DatabaseIdentity,
            static row => row.Id, static (row, key) => row.Id = key);
        var command = BuildCommand();
        if (command.Sql != sql || !command.HasReturning || !command.IsGeneratedExecution ||
            UseGeneratedSql && !ReferenceEquals(command.Sql, sql))
            throw new InvalidOperationException("Generated key SQL benchmark did not reach the expected production path.");
    }

    /// <summary>与 Executor 一样为每条命令使用独立可变 Builder，服务及映射计划共享。</summary>
    [Benchmark]
    public SqlWriteCommand BuildCommand() => new SqlKeyInsertCommandBuilder(
        new SqlInsertBuilder(SqliteSqlProvider.Instance, _services), _policy).Build(_row);

    internal static int RunAllocationProbe(string output)
    {
        const int iterations = 10000;
        var results = new List<object>();
        foreach (var empty in new[] { false, true })
        foreach (var generated in new[] { false, true })
        {
            var benchmark = new SqlGeneratedKeyInsertBenchmarks { DefaultValues = empty, UseGeneratedSql = generated };
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
            results.Add(new { defaultValues = empty, generatedSql = generated, allocatedBytesPerOperation = samples });
        }
        var report = new { probe = "GeneratedKeyInsertSql-v1", runtime = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), warmup = 2048, iterations, results };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
        Console.WriteLine("GENERATED_KEY_INSERT_ALLOCATION_PROBE_PASS");
        return 0;
    }

    private sealed class Row { public long Id { get; set; } public string Name { get; set; } }
}
