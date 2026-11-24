using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Dapper.MySql;
using Bing.Dapper.PostgreSql;
using Bing.Dapper.SqlServer;
using Bing.Dapper.Sqlite;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>测量单键组合 Delete 的动态 SQL 与形状 SQL 命令构建分配。</summary>
[MemoryDiagnoser]
public class SqlGeneratedBatchDeleteBenchmarks
{
    private const string TableName = "generated_batch_delete_rows";
    private Row[] _rows = Array.Empty<Row>();
    private DefaultSqlEntityMutationCommandBuilder _builder;
    private ISqlProvider _provider;
    private string _shapeHead;
    private string _shapePrefix;
    private bool _generatedSqlPathReached;

    /// <summary>当前组合删除所使用的数据库 Provider。</summary>
    [Params("SQLite", "MySQL", "PostgreSQL", "SqlServer")]
    public string Provider { get; set; } = "SQLite";

    /// <summary>是否向元数据登记完整形状 SQL。</summary>
    [Params(false, true)]
    public bool UseGeneratedSql { get; set; }

    /// <summary>单次组合删除的实体数量。</summary>
    [Params(3, 12)]
    public int RowCount { get; set; }

    /// <summary>创建同一映射和 Getter 下的动态及候选生成命令，并核对完整执行语义。</summary>
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
        _rows = Enumerable.Range(1, RowCount).Select(id => new Row { Id = id }).ToArray();
        _shapeHead = $"Delete From {_provider.Dialect.SafeName(TableName)} Where {_provider.Dialect.SafeName("Id")} In (";
        _shapePrefix = "@_p_";

        var dynamicCommand = CreateBuilder(registerShapeSql: false).DeleteCombined(_rows);
        var expectedSql = dynamicCommand.Sql;
        _builder = CreateBuilder(registerShapeSql: UseGeneratedSql);
        var command = _builder.DeleteCombined(_rows);
        AssertEquivalent(dynamicCommand, command);
        if (UseGeneratedSql && command.Sql != expectedSql)
            throw new InvalidOperationException("Generated batch-delete SQL differs from the complete dynamic SQL.");
    }

    /// <summary>构造组合删除命令，不包含数据库 IO。</summary>
    [Benchmark]
    public SqlWriteCommand BuildCommand() => _builder.DeleteCombined(_rows);

    /// <summary>执行固定 2048 次预热及五组 5000 次分配采样。</summary>
    public static int RunAllocationProbe(string output)
    {
        const int warmupIterations = 2048;
        const int measuredIterations = 5000;
        const int sampleCount = 5;
        var results = new List<object>();
        foreach (var provider in new[] { "SQLite", "MySQL", "PostgreSQL", "SqlServer" })
        foreach (var rowCount in new[] { 3, 12 })
        foreach (var generated in new[] { false, true })
        {
            var benchmark = new SqlGeneratedBatchDeleteBenchmarks
            {
                Provider = provider,
                RowCount = rowCount,
                UseGeneratedSql = generated
            };
            benchmark.Setup();
            for (var i = 0; i < warmupIterations; i++)
                benchmark.BuildCommand();

            var allocatedBytesPerOperation = new double[sampleCount];
            SqlWriteCommand last = null;
            for (var sample = 0; sample < sampleCount; sample++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var iteration = 0; iteration < measuredIterations; iteration++)
                    last = benchmark.BuildCommand();
                allocatedBytesPerOperation[sample] =
                    (GC.GetAllocatedBytesForCurrentThread() - before) / (double)measuredIterations;
            }
            GC.KeepAlive(last);
            results.Add(new
            {
                provider,
                rowCount,
                generatedSqlRequested = generated,
                generatedSqlPathReached = benchmark._generatedSqlPathReached,
                allocatedBytesPerOperation
            });
        }

        var report = new
        {
            probe = "GeneratedBatchDeleteAllocation-v1",
            runtime = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            warmupIterations,
            measuredIterations,
            sampleCount,
            results
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
        Console.WriteLine("GENERATED_BATCH_DELETE_ALLOCATION_PROBE_PASS");
        return 0;
    }

    private DefaultSqlEntityMutationCommandBuilder CreateBuilder(bool registerShapeSql)
    {
        var model = new EntityModelMetadata(typeof(Row), TableName, null, new[]
        {
            EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true, isKey: true,
                generatedGetter: source => ((Row)source).Id)
        }, () => new Row())
        {
            GeneratedMutationShapeSql = registerShapeSql ? GetShapeSql : null
        };
        return new DefaultSqlEntityMutationCommandBuilder(_provider, new SqlBuilderServices(
            metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model)));
    }

    private string GetShapeSql(string provider, SqlOperationKind operation, string property, int rows)
    {
        if (provider != _provider.Key || operation != SqlOperationKind.Delete || property != null || rows <= 1)
            return null;

        _generatedSqlPathReached = true;
        var sql = new StringBuilder(_shapeHead);
        for (var row = 0; row < rows; row++)
        {
            if (row > 0)
                sql.Append(',');
            sql.Append(_shapePrefix).Append(row.ToString(CultureInfo.InvariantCulture));
        }
        return sql.Append(')').ToString();
    }

    private static void AssertEquivalent(SqlWriteCommand expected, SqlWriteCommand actual)
    {
        if (expected.Sql != actual.Sql)
            throw new InvalidOperationException($"Dynamic and shape SQL differ. Expected: {expected.Sql}; actual: {actual.Sql}");
        if (expected.ProviderKey != actual.ProviderKey || expected.OperationKind != actual.OperationKind ||
            expected.HasReturning != actual.HasReturning || expected.ValidateAffectedRows != actual.ValidateAffectedRows ||
            expected.IsGeneratedExecution != actual.IsGeneratedExecution)
            throw new InvalidOperationException($"Dynamic and shape SQL flags differ. Provider {expected.ProviderKey}/{actual.ProviderKey}; " +
                $"operation {expected.OperationKind}/{actual.OperationKind}; returning {expected.HasReturning}/{actual.HasReturning}; " +
                $"validateRows {expected.ValidateAffectedRows}/{actual.ValidateAffectedRows}; " +
                $"generatedExecution {expected.IsGeneratedExecution}/{actual.IsGeneratedExecution}.");

        var expectedParameters = expected.Parameters.ToArray();
        var actualParameters = actual.Parameters.ToArray();
        if (expectedParameters.Length != actualParameters.Length)
            throw new InvalidOperationException("Dynamic and shape SQL commands have different parameter counts.");
        for (var index = 0; index < expectedParameters.Length; index++)
            if (!Equivalent(expectedParameters[index], actualParameters[index]))
                throw new InvalidOperationException($"Dynamic and shape SQL parameter {index} differs.");
    }

    private static bool Equivalent(SqlParam left, SqlParam right) =>
        left.Name == right.Name && Equals(left.Value, right.Value) && Equals(left.OriginalValue, right.OriginalValue) &&
        left.IsProviderValue == right.IsProviderValue && left.Direction == right.Direction && left.DbType == right.DbType &&
        left.Size == right.Size && left.Precision == right.Precision && left.Scale == right.Scale &&
        left.EntityType == right.EntityType && left.PropertyName == right.PropertyName && left.ColumnName == right.ColumnName &&
        left.DatabaseType == right.DatabaseType && left.ProviderTypeName == right.ProviderTypeName && left.Source == right.Source &&
        left.MetadataLevel == right.MetadataLevel && left.StorageKind == right.StorageKind &&
        left.ConverterKind == right.ConverterKind && left.CustomConverterName == right.CustomConverterName;

    private sealed class Row
    {
        public int Id { get; init; }
    }
}
