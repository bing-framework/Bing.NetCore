using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Benchmarks.Models;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>比较多行筛选列 InsertCombined 的生成 SQL 与 Builder 回退分配。</summary>
[MemoryDiagnoser]
public class SqlGeneratedMutationRowsBenchmarks
{
    private static readonly string[] IncludedProperties =
        { nameof(SelectedMutationColumnsRow.Name), nameof(SelectedMutationColumnsRow.Price), nameof(SelectedMutationColumnsRow.Code) };
    private static readonly string[] ExcludedProperties =
        { nameof(SelectedMutationColumnsRow.Notes), nameof(SelectedMutationColumnsRow.Version) };

    private SelectedMutationColumnsRow[] _rows = Array.Empty<SelectedMutationColumnsRow>();
    private SqlInsertOptions _options;
    private DefaultSqlEntityMutationCommandBuilder _generatedBuilder;
    private DefaultSqlEntityMutationCommandBuilder _fallbackBuilder;

    /// <summary>组合插入的实体行数。</summary>
    [Params(3, 12)]
    public int RowCount { get; set; }

    /// <summary>写列筛选方式。</summary>
    [Params("Include", "Exclude")]
    public string Selection { get; set; } = "Include";

    /// <summary>准备实际源生成模型和仅禁用筛选 SQL 工厂的回退模型，并校验完整命令。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _rows = Enumerable.Range(1, RowCount).Select(index => new SelectedMutationColumnsRow
        {
            Id = index,
            Name = "widget-" + index.ToString(CultureInfo.InvariantCulture),
            Price = index + 0.25m,
            Code = "W-" + index.ToString(CultureInfo.InvariantCulture),
            Notes = "excluded-" + index.ToString(CultureInfo.InvariantCulture),
            Version = index,
            Calculated = index * 10
        }).ToArray();
        _options = Selection == "Include"
            ? new SqlInsertOptions { IncludeProperties = IncludedProperties }
            : new SqlInsertOptions { ExcludeProperties = ExcludedProperties };

        var generatedModel = SelectedMutationColumnsContext.CreateModels().Single();
        var columnsFactory = generatedModel.GeneratedMutationColumnsSql ??
            throw new InvalidOperationException("源生成模型未提供筛选列 SQL 工厂。");
        var expectedSql = CreateExpectedSql(RowCount);
        var expectedParameters = CreateExpectedParameters(_rows);
        var factoryHits = 0;
        var instrumentedModel = SqlGeneratedMutationColumnsBenchmarks.CopyModel(generatedModel,
            (provider, operation, properties, rows, returning) =>
            {
                factoryHits++;
                return columnsFactory(provider, operation, properties, rows, returning);
            });

        _generatedBuilder = SqlGeneratedMutationColumnsBenchmarks.CreateBuilder(instrumentedModel);
        _fallbackBuilder = SqlGeneratedMutationColumnsBenchmarks.CreateBuilder(
            SqlGeneratedMutationColumnsBenchmarks.CopyModel(generatedModel, null));
        var generatedCommand = BuildCombinedInsert(_generatedBuilder);
        if (factoryHits != 1)
            throw new InvalidOperationException("筛选列 SQL 工厂未恰好命中一次。");
        var fallbackCommand = BuildCombinedInsert(_fallbackBuilder);

        SqlGeneratedMutationColumnsBenchmarks.ValidateCommand(generatedCommand, expectedSql, expectedParameters,
            returning: false);
        SqlGeneratedMutationColumnsBenchmarks.ValidateCommand(fallbackCommand, expectedSql, expectedParameters,
            returning: false);
        if (!generatedCommand.GeneratedParameterBinder.Equals(fallbackCommand.GeneratedParameterBinder))
            throw new InvalidOperationException("生成 SQL 和 Builder 回退必须使用同一个静态参数绑定器。");
        // 工厂命中检查只用于 Setup；正式测量直接使用生成委托，避免计数器影响计时。
        _generatedBuilder = SqlGeneratedMutationColumnsBenchmarks.CreateBuilder(generatedModel);
    }

    /// <summary>使用真实源生成的筛选列 SQL 工厂构造多行命令，不执行数据库 I/O。</summary>
    [Benchmark]
    public SqlWriteCommand BuildGeneratedColumns() => BuildCombinedInsert(_generatedBuilder);

    /// <summary>仅关闭筛选列 SQL 工厂，保留完整生成映射和静态绑定器构造多行命令。</summary>
    [Benchmark(Baseline = true)]
    public SqlWriteCommand BuildBuilderFallback() => BuildCombinedInsert(_fallbackBuilder);

    /// <summary>运行固定预热和五组分配采样，并将 UTF-8 JSON 写入调用方指定路径。</summary>
    internal static int RunAllocationProbe(string output)
    {
        const int warmupIterations = 2048;
        const int sampleCount = 5;
        const int measuredIterations = 5000;
        var results = new List<object>();

        foreach (var rowCount in new[] { 3, 12 })
        foreach (var selection in new[] { "Include", "Exclude" })
        {
            var benchmark = new SqlGeneratedMutationRowsBenchmarks
            {
                RowCount = rowCount,
                Selection = selection
            };
            benchmark.Setup();

            foreach (var path in new[] { "GeneratedMutationColumnsSql", "BuilderFallback" })
            {
                Func<SqlWriteCommand> action = path == "GeneratedMutationColumnsSql"
                    ? benchmark.BuildGeneratedColumns
                    : benchmark.BuildBuilderFallback;
                SqlWriteCommand last = null;
                for (var index = 0; index < warmupIterations; index++)
                    last = action();

                var allocatedBytesPerOperation = new double[sampleCount];
                for (var sample = 0; sample < sampleCount; sample++)
                {
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    for (var index = 0; index < measuredIterations; index++)
                        last = action();
                    allocatedBytesPerOperation[sample] =
                        (GC.GetAllocatedBytesForCurrentThread() - before) / (double)measuredIterations;
                }

                GC.KeepAlive(last);
                var parameters = last.Parameters.ToArray();
                results.Add(new
                {
                    provider = SqliteSqlProvider.Instance.Key,
                    rowCount,
                    selection,
                    callPath = path,
                    parameterNames = parameters.Select(parameter => parameter.Name).ToArray(),
                    parameterValues = parameters.Select(parameter => parameter.Value).ToArray(),
                    allocatedBytesPerOperation
                });
            }
        }

        var report = new
        {
            probe = "GeneratedMutationRowsAllocation-v1",
            runtime = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            warmupIterations,
            sampleCount,
            measuredIterations,
            results
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
        return 0;
    }

    private SqlWriteCommand BuildCombinedInsert(DefaultSqlEntityMutationCommandBuilder builder) =>
        ((ISqlCombinedInsertMutationBuilder)builder).InsertCombined(_rows, _options);

    private static string CreateExpectedSql(int rowCount)
    {
        var values = Enumerable.Range(0, rowCount).Select(row =>
        {
            var first = row * 3;
            return "(@_p_" + first.ToString(CultureInfo.InvariantCulture) + ", @_p_" +
                (first + 1).ToString(CultureInfo.InvariantCulture) + ", @_p_" +
                (first + 2).ToString(CultureInfo.InvariantCulture) + ")";
        });
        return "Insert Into `selected_mutation_rows` (`display_name`, `unit_price`, `stock_code`) Values " +
            string.Join(", ", values);
    }

    private static IReadOnlyList<object> CreateExpectedParameters(IEnumerable<SelectedMutationColumnsRow> rows)
    {
        var values = new List<object>();
        foreach (var row in rows)
        {
            values.Add(row.Name);
            values.Add(row.Price);
            values.Add(row.Code);
        }
        return values;
    }
}
