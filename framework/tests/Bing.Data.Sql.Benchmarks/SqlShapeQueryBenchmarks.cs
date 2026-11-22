using System;
using System.Data;
using BenchmarkDotNet.Attributes;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>比较查询形状直接绑定与显式渲染后的查询描述分配。</summary>
[MemoryDiagnoser]
public class SqlShapeQueryBenchmarks
{
    private ServiceProvider _services;
    private SqliteConnection _connection;
    private SqliteSqlQuery _query;
    private SqlQueryShapeTemplate _template;
    private SqlParam[] _parameterDefinitions;
    private object[] _values;
    private string _commandText;

    /// <summary>查询参数槽位数，包含二进制槽位。</summary>
    [Params(1, 9)]
    public int ParameterCount { get; set; }

    /// <summary>二进制参数长度。</summary>
    [Params(0, 4096)]
    public int PayloadBytes { get; set; }

    /// <summary>构造未打开的 SQLite 查询根对象，预热形状并核对两种绑定结果。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _services = new ServiceCollection().AddSqliteProvider().BuildServiceProvider();
        _connection = new SqliteConnection("Data Source=:memory:");
        _query = new SqliteSqlQuery(_services, new SqlOptions<SqliteSqlQuery>
        {
            DatabaseType = DatabaseType.Sqlite,
            Connection = _connection
        });

        var payload = new byte[PayloadBytes];
        _values = new object[ParameterCount];
        _parameterDefinitions = new SqlParam[ParameterCount];
        var names = new string[ParameterCount];
        for (var index = 0; index < ParameterCount; index++)
        {
            var isPayload = index == 0;
            var name = isPayload ? "@payload" : "@p" + index;
            var value = isPayload ? payload : (object)(index * 17);
            names[index] = name;
            _values[index] = value;
            _parameterDefinitions[index] = new SqlParam(name,
                isPayload ? (object)Array.Empty<byte>() : 0,
                isPayload ? DbType.Binary : DbType.Int32, ParameterDirection.Input,
                isPayload ? PayloadBytes : sizeof(int),
                isPayload ? (byte?)null : (byte)9, isPayload ? (byte?)null : (byte)0)
            {
                EntityType = typeof(SqlShapeQueryBenchmarks),
                PropertyName = isPayload ? nameof(PayloadBytes) : "Value" + index,
                ColumnName = isPayload ? "payload" : "value_" + index,
                DatabaseType = DatabaseType.Sqlite,
                ProviderTypeName = isPayload ? "BLOB" : "INTEGER",
                Source = SqlParameterSource.Manual,
                MetadataLevel = SqlParameterMetadataLevel.Full,
                StorageKind = isPayload ? ColumnStorageKind.Binary : ColumnStorageKind.Number,
                ConverterKind = FieldValueConverterKind.None
            };
        }

        _commandText = "SELECT " + string.Join(", ", names);
        _template = new SqlQueryShapeTemplate(new SqlQueryFragment(_commandText, _parameterDefinitions));

        var direct = _query.Sql(_template, 0, _values);
        var fragment = _template.RenderValues(0, _values);
        var rendered = _query.Sql(fragment.Sql, fragment.Parameters);
        VerifyEquivalent(direct, rendered, _commandText, _parameterDefinitions, _values);
    }

    /// <summary>由查询入口渲染形状并创建查询描述。</summary>
    [Benchmark]
    public SqlTextQuery DirectQueryShape() => _query.Sql(_template, 0, _values);

    /// <summary>先显式渲染片段，再通过原生 SQL 入口创建查询描述。</summary>
    [Benchmark(Baseline = true)]
    public SqlTextQuery RenderThenQuery()
    {
        var fragment = _template.RenderValues(0, _values);
        return _query.Sql(fragment.Sql, fragment.Parameters);
    }

    /// <summary>释放查询、未打开连接和服务容器。</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _query?.Dispose();
        _connection?.Dispose();
        _services?.Dispose();
    }

    private static void VerifyEquivalent(SqlTextQuery direct, SqlTextQuery rendered,
        string expectedCommandText, SqlParam[] definitions, object[] expectedValues)
    {
        if (direct.CommandText != expectedCommandText || rendered.CommandText != expectedCommandText)
            throw new InvalidOperationException("查询形状基准生成的完整 SQL 不一致。");

        if (direct.Parameters is not SqlParam[] directParameters ||
            rendered.Parameters is not SqlParam[] renderedParameters ||
            directParameters.Length != definitions.Length || renderedParameters.Length != definitions.Length)
            throw new InvalidOperationException("查询形状基准生成的参数数量或类型不一致。");

        for (var index = 0; index < definitions.Length; index++)
        {
            var directParameter = directParameters[index];
            var renderedParameter = renderedParameters[index];
            if (!MetadataEquals(definitions[index], directParameter) ||
                !MetadataEquals(definitions[index], renderedParameter) ||
                !MetadataEquals(directParameter, renderedParameter) ||
                !ValueEquals(expectedValues[index], directParameter.Value) ||
                !ValueEquals(expectedValues[index], renderedParameter.Value))
                throw new InvalidOperationException("查询形状基准生成的参数值或元数据不一致。");
        }
    }

    private static bool ValueEquals(object expected, object actual)
    {
        if (expected is byte[] expectedBytes && actual is byte[] actualBytes)
            return expectedBytes.AsSpan().SequenceEqual(actualBytes);
        return Equals(expected, actual);
    }

    private static bool MetadataEquals(SqlParam expected, SqlParam actual) =>
        expected.Name == actual.Name &&
        expected.Direction == actual.Direction &&
        expected.DbType == actual.DbType &&
        expected.Size == actual.Size &&
        expected.Precision == actual.Precision &&
        expected.Scale == actual.Scale &&
        expected.EntityType == actual.EntityType &&
        expected.PropertyName == actual.PropertyName &&
        expected.ColumnName == actual.ColumnName &&
        expected.DatabaseType == actual.DatabaseType &&
        expected.ProviderTypeName == actual.ProviderTypeName &&
        expected.Source == actual.Source &&
        expected.MetadataLevel == actual.MetadataLevel &&
        expected.StorageKind == actual.StorageKind &&
        expected.ConverterKind == actual.ConverterKind &&
        expected.CustomConverterName == actual.CustomConverterName;
}
