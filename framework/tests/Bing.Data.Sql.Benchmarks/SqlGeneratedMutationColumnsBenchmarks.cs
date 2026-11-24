using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Benchmarks.Models;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>比较多列筛选写入的生成式 SQL 片段与 Builder 回退路径。</summary>
[MemoryDiagnoser]
public class SqlGeneratedMutationColumnsBenchmarks
{
    private static readonly string[] IncludedProperties = { "Name", "Price", "Code" };
    private static readonly string[] ExcludedProperties = { "Notes", "Version" };
    private readonly SelectedMutationColumnsRow _row = new()
    {
        Id = 41,
        Name = "widget",
        Price = 12.50m,
        Code = "W-17",
        Notes = "excluded",
        Version = 9,
        Calculated = 99
    };

    private DefaultSqlEntityMutationCommandBuilder _generatedBuilder;
    private DefaultSqlEntityMutationCommandBuilder _fallbackBuilder;
    private SqlInsertOptions _insertOptions;
    private SqlUpdateOptions _updateOptions;

    /// <summary>单实体写操作。</summary>
    [Params("Insert", "Update")]
    public string Operation { get; set; } = "Insert";

    /// <summary>多列筛选方式。</summary>
    [Params("Include", "Exclude")]
    public string Selection { get; set; } = "Include";

    /// <summary>是否通过 Returning 返回整行。</summary>
    [Params(false, true)]
    public bool Returning { get; set; }

    /// <summary>创建源生成模型及动态回退模型，核对 SQL、参数和静态绑定器。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _insertOptions = Selection == "Include"
            ? new SqlInsertOptions { IncludeProperties = IncludedProperties }
            : new SqlInsertOptions { ExcludeProperties = ExcludedProperties };
        _updateOptions = Selection == "Include"
            ? new SqlUpdateOptions { IncludeProperties = IncludedProperties }
            : new SqlUpdateOptions { ExcludeProperties = ExcludedProperties };

        var generatedModel = SelectedMutationColumnsContext.CreateModels().Single();
        var columnsFactory = generatedModel.GeneratedMutationColumnsSql ??
            throw new InvalidOperationException("The source generator did not create GeneratedMutationColumnsSql.");
        var writeProperties = IncludedProperties;
        var operationKind = Operation == "Insert" ? SqlOperationKind.InsertValues : SqlOperationKind.Update;
        var expectedSql = GetExpectedSql(Operation, Returning);
        var expectedParameters = GetExpectedParameterValues(Operation);
        var factorySql = columnsFactory(SqliteSqlProvider.Instance.Key, operationKind, writeProperties, 1, Returning);
        if (factorySql == null || factorySql != expectedSql)
            throw new InvalidOperationException("Generated selected-column SQL did not match the full expected statement.");

        var factoryHits = 0;
        var instrumentedModel = CopyModel(generatedModel, (provider, operation, properties, rows, returning) =>
        {
            factoryHits++;
            return columnsFactory(provider, operation, properties, rows, returning);
        });
        var instrumentedCommand = BuildCommand(CreateBuilder(instrumentedModel));
        if (factoryHits != 1)
            throw new InvalidOperationException("The selected-column SQL factory was not used exactly once.");
        ValidateCommand(instrumentedCommand, expectedSql, expectedParameters, Returning);

        var fallbackModel = CopyModel(generatedModel, null);
        _generatedBuilder = CreateBuilder(generatedModel);
        _fallbackBuilder = CreateBuilder(fallbackModel);
        var generatedCommand = BuildCommand(_generatedBuilder);
        var fallbackCommand = BuildCommand(_fallbackBuilder);
        ValidateCommand(generatedCommand, expectedSql, expectedParameters, Returning);
        ValidateCommand(fallbackCommand, expectedSql, expectedParameters, Returning);
        if (!generatedCommand.GeneratedParameterBinder.Equals(fallbackCommand.GeneratedParameterBinder))
            throw new InvalidOperationException("Generated and Builder paths must use the same static parameter binder.");

        var defaultSql = Returning
            ? generatedModel.GeneratedMutationReturningSql?.Invoke(SqliteSqlProvider.Instance.Key, operationKind)
            : generatedModel.GeneratedMutationSql?.Invoke(SqliteSqlProvider.Instance.Key, operationKind);
        if (defaultSql == expectedSql)
            throw new InvalidOperationException("The benchmark selection must differ from the default full-column shape.");
    }

    /// <summary>使用完整映射及筛选列 SQL 工厂构造命令，不执行数据库 I/O。</summary>
    [Benchmark]
    public SqlWriteCommand BuildGeneratedColumns() => BuildCommand(_generatedBuilder);

    /// <summary>保留相同生成 Getter 和绑定器，仅通过 Builder 拼接筛选列 SQL。</summary>
    [Benchmark(Baseline = true)]
    public SqlWriteCommand BuildBuilderFallback() => BuildCommand(_fallbackBuilder);

    /// <summary>运行固定 5 组、每组 5000 次分配探针；预热命令不计入采样。</summary>
    internal static int RunAllocationProbe(string output)
    {
        const int warmupIterations = 2048;
        const int sampleCount = 5;
        const int measuredIterations = 5000;
        var results = new List<object>();

        foreach (var operation in new[] { "Insert", "Update" })
        foreach (var selection in new[] { "Include", "Exclude" })
        foreach (var returning in new[] { false, true })
        {
            var benchmark = new SqlGeneratedMutationColumnsBenchmarks
            {
                Operation = operation,
                Selection = selection,
                Returning = returning
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

                var samples = new double[sampleCount];
                for (var sample = 0; sample < sampleCount; sample++)
                {
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    for (var index = 0; index < measuredIterations; index++)
                        last = action();
                    samples[sample] = (GC.GetAllocatedBytesForCurrentThread() - before) /
                        (double)measuredIterations;
                }

                var parameters = last.Parameters.ToArray();
                results.Add(new
                {
                    provider = SqliteSqlProvider.Instance.Key,
                    operation,
                    selection,
                    returning,
                    callPath = path,
                    selectedProperties = IncludedProperties,
                    selectionProperties = selection == "Include" ? IncludedProperties : ExcludedProperties,
                    parameterNames = parameters.Select(parameter => parameter.Name).ToArray(),
                    parameterValues = parameters.Select(parameter => parameter.Value).ToArray(),
                    allocatedBytesPerOperation = samples
                });
            }
        }

        var report = new
        {
            probe = "GeneratedMutationColumnsSql-v1",
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

    private SqlWriteCommand BuildCommand(DefaultSqlEntityMutationCommandBuilder builder) =>
        (Operation, Returning) switch
        {
            ("Insert", false) => builder.Insert(_row, _insertOptions),
            ("Insert", true) => builder.InsertReturning(_row, _insertOptions),
            ("Update", false) => builder.Update(_row, _updateOptions),
            ("Update", true) => builder.UpdateReturning(_row, _updateOptions),
            _ => throw new InvalidOperationException("Unknown mutation benchmark parameters.")
        };

    internal static DefaultSqlEntityMutationCommandBuilder CreateBuilder(EntityModelMetadata model) =>
        new(SqliteSqlProvider.Instance, new SqlBuilderServices(
            metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model)));

    internal static EntityModelMetadata CopyModel(EntityModelMetadata source,
        Func<string, SqlOperationKind, IReadOnlyList<string>, int, bool, string> columnsFactory) =>
        new(source.EntityType, source.TableName, source.Schema, source.Properties.Values,
            source.GeneratedFactory, source.GeneratedValueReader)
        {
            GeneratedMutationSql = source.GeneratedMutationSql,
            GeneratedKeyInsertSql = source.GeneratedKeyInsertSql,
            GeneratedMutationShapeSql = source.GeneratedMutationShapeSql,
            GeneratedMutationReturningSql = source.GeneratedMutationReturningSql,
            GeneratedMutationColumnsSql = columnsFactory
        };

    internal static void ValidateCommand(SqlWriteCommand command, string expectedSql,
        IReadOnlyList<object> expectedParameters, bool returning)
    {
        if (command.Sql != expectedSql || command.HasReturning != returning || !command.IsGeneratedExecution ||
            command.GeneratedParameterBinder == null)
            throw new InvalidOperationException("Mutation command SQL, Returning, or generated binding did not match setup.");

        var actualParameters = command.Parameters.ToArray();
        if (actualParameters.Length != expectedParameters.Count)
            throw new InvalidOperationException("Mutation command parameter count did not match setup.");
        for (var index = 0; index < actualParameters.Length; index++)
            if (actualParameters[index].Name != "@_p_" + index ||
                !Equals(actualParameters[index].Value, expectedParameters[index]))
                throw new InvalidOperationException("Mutation command parameter names or values did not match setup.");
    }

    private static IReadOnlyList<object> GetExpectedParameterValues(string operation)
    {
        var values = new List<object> { "widget", 12.50m, "W-17" };
        if (operation == "Update")
        {
            values.Add(41);
            values.Add(9);
        }
        return values;
    }

    private static string GetExpectedSql(string operation, bool returning)
    {
        var sql = operation switch
        {
            "Insert" => "Insert Into `selected_mutation_rows` (`display_name`, `unit_price`, `stock_code`) " +
                "Values (@_p_0, @_p_1, @_p_2)",
            "Update" => "Update `selected_mutation_rows` Set `display_name` = @_p_0, `unit_price` = @_p_1, " +
                "`stock_code` = @_p_2 Where `row_id` = @_p_3 And `row_version` = @_p_4",
            _ => throw new InvalidOperationException("Unknown mutation operation.")
        };
        if (!returning)
            return sql;
        const string returnedColumns = "`row_id` As `Id`, `display_name` As `Name`, `unit_price` As `Price`, " +
            "`stock_code` As `Code`, `notes` As `Notes`, `row_version` As `Version`, " +
            "`calculated_value` As `Calculated`";
        return sql + " Returning " + returnedColumns;
    }
}
