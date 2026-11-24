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

/// <summary>测量生成式复合键与并发令牌组合删除的构建分配。</summary>
[MemoryDiagnoser]
public class SqlGeneratedPairedDeleteBenchmarks
{
    private const string TableName = "paired_delete_rows";
    private Row[] _rows = Array.Empty<Row>();
    private DefaultSqlEntityMutationCommandBuilder _builder;
    private ISqlProvider _provider;
    private string _shapeHead;
    private string _idColumn;
    private string _tenantColumn;
    private string _versionColumn;
    private bool _generatedSqlPathReached;

    /// <summary>当前删除命令使用的数据库 Provider。</summary>
    [Params("SQLite", "MySQL", "PostgreSQL", "SqlServer")]
    public string Provider { get; set; } = "SQLite";

    /// <summary>是否登记生成的配对 SQL 形状。</summary>
    [Params(false, true)]
    public bool UseGeneratedSql { get; set; }

    /// <summary>组合删除中的实体数量。</summary>
    [Params(3, 12)]
    public int RowCount { get; set; }

    /// <summary>建立动态与生成候选命令，并验证 SQL、参数和执行标记。</summary>
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

        _rows = Enumerable.Range(1, RowCount).Select(id => new Row
        {
            Id = id,
            TenantId = 1000 + id,
            Version = 2000 + id
        }).ToArray();
        _shapeHead = $"Delete From {_provider.Dialect.SafeName(TableName)} Where (";
        _idColumn = _provider.Dialect.SafeName(nameof(Row.Id));
        _tenantColumn = _provider.Dialect.SafeName(nameof(Row.TenantId));
        _versionColumn = _provider.Dialect.SafeName(nameof(Row.Version));

        var dynamicCommand = CreateBuilder(registerShapeSql: false).DeleteCombined(_rows);
        _builder = CreateBuilder(registerShapeSql: UseGeneratedSql);
        var command = _builder.DeleteCombined(_rows);
        AssertEquivalent(dynamicCommand, command);
        if (!command.ValidateAffectedRows)
            throw new InvalidOperationException("Paired delete must validate affected rows for its concurrency token.");
    }

    /// <summary>构建组合删除命令，不执行数据库 IO。</summary>
    [Benchmark]
    public SqlWriteCommand BuildCommand() => _builder.DeleteCombined(_rows);

    /// <summary>以固定工作量输出每种组合的分配字节数。</summary>
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
            var benchmark = new SqlGeneratedPairedDeleteBenchmarks
            {
                Provider = provider,
                RowCount = rowCount,
                UseGeneratedSql = generated
            };
            benchmark.Setup();
            for (var iteration = 0; iteration < warmupIterations; iteration++)
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
            probe = "GeneratedPairedDeleteAllocation-v1",
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
        Console.WriteLine("GENERATED_PAIRED_DELETE_ALLOCATION_PROBE_PASS");
        return 0;
    }

    private DefaultSqlEntityMutationCommandBuilder CreateBuilder(bool registerShapeSql)
    {
        var model = new EntityModelMetadata(typeof(Row), TableName, null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int?), true, true, isKey: true,
                generatedGetter: static source => ((Row)source).Id),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.TenantId), typeof(int?), true, true, isKey: true,
                generatedGetter: static source => ((Row)source).TenantId),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Version), typeof(int?), true, true, isConcurrencyToken: true,
                generatedGetter: static source => ((Row)source).Version)
        }, static () => new Row())
        {
            GeneratedMutationShapeSql = registerShapeSql ? CreateShapeSql : null
        };

        var options = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        var mapping = new EntityMappingOptions { EntityType = typeof(Row), TableName = TableName };
        foreach (var name in new[] { nameof(Row.Id), nameof(Row.TenantId), nameof(Row.Version) })
            mapping.Columns[name] = new ColumnMappingOptions { PropertyName = name, DbType = System.Data.DbType.Int32 };
        options.EntityMappings.Add(mapping);

        return new DefaultSqlEntityMutationCommandBuilder(_provider, new SqlBuilderServices(
            metadataOptions: options,
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model)));
    }

    private string CreateShapeSql(string providerKey, SqlOperationKind operation, string property, int rows)
    {
        if (providerKey != _provider.Key || operation != SqlOperationKind.Delete || property != null || rows <= 1)
            return null;

        _generatedSqlPathReached = true;
        var sql = new StringBuilder(_shapeHead);
        for (var depth = 2; depth < rows; depth++)
            sql.Append('(');
        for (var row = 0; row < rows; row++)
        {
            if (row > 0)
                sql.Append(" Or ");
            WritePairedRow(sql, row * 3, _idColumn, _tenantColumn, _versionColumn);
            if (row > 0)
                sql.Append(')');
        }
        return sql.ToString();
    }

    private static void WritePairedRow(StringBuilder sql, int firstParameter, string idColumn, string tenantColumn,
        string versionColumn)
    {
        sql.Append('(')
            .Append(idColumn).Append(" = @_p_").Append(firstParameter.ToString(CultureInfo.InvariantCulture))
            .Append(" And ").Append(tenantColumn).Append(" = @_p_")
            .Append((firstParameter + 1).ToString(CultureInfo.InvariantCulture))
            .Append(" And ").Append(versionColumn).Append(" = @_p_")
            .Append((firstParameter + 2).ToString(CultureInfo.InvariantCulture))
            .Append(')');
    }

    private static void AssertEquivalent(SqlWriteCommand expected, SqlWriteCommand actual)
    {
        if (expected.Sql != actual.Sql)
            throw new InvalidOperationException($"Dynamic and generated SQL differ. Expected: {expected.Sql}; actual: {actual.Sql}");
        if (expected.ProviderKey != actual.ProviderKey || expected.OperationKind != actual.OperationKind ||
            expected.HasReturning != actual.HasReturning || expected.ValidateAffectedRows != actual.ValidateAffectedRows ||
            expected.IsGeneratedExecution != actual.IsGeneratedExecution)
            throw new InvalidOperationException("Dynamic and generated commands have different execution flags.");

        var expectedParameters = expected.Parameters.ToArray();
        var actualParameters = actual.Parameters.ToArray();
        if (expectedParameters.Length != actualParameters.Length)
            throw new InvalidOperationException("Dynamic and generated commands have different parameter counts.");
        for (var index = 0; index < expectedParameters.Length; index++)
            if (!Equivalent(expectedParameters[index], actualParameters[index]))
                throw new InvalidOperationException($"Dynamic and generated parameter {index} differs.");
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
        public int? Id { get; init; }
        public int? TenantId { get; init; }
        public int? Version { get; init; }
    }
}
