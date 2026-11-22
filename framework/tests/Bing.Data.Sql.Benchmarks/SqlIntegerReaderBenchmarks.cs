using System;
using System.Data.Common;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Metadata;
using Microsoft.Data.Sqlite;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>相同 SQLite INTEGER 值的旧转换路径与生成式直接赋值形态；不包含查询、绑定和结果对象创建。</summary>
[MemoryDiagnoser]
public class SqlIntegerReaderBenchmarks
{
    private SqliteConnection _connection;
    private SqliteCommand _command;
    private DbDataReader _reader;
    private readonly Row _row = new();
    private EntityPropertyMetadata _member;

    [GlobalSetup]
    public void Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _command = _connection.CreateCommand();
        _command.CommandText = "SELECT 32767";
        _reader = _command.ExecuteReader();
        if (!_reader.Read() || _reader.GetFieldType(0) != typeof(long))
            throw new InvalidOperationException("Expected SQLite Int64 storage.");
        // 手写委托与生成器输出等形；真实生成器由职责测试和 Native Smoke 独立验证。
        _member = EntityPropertyMetadata.CreateGenerated(nameof(Row.Value), typeof(short), true, true,
            generatedSetter: static (instance, value) => ((Row)instance).Value = (short)value)
            .WithGeneratedReader(static (instance, reader, ordinal) =>
            {
                if (reader.GetFieldType(ordinal) == typeof(long))
                {
                    ((Row)instance).Value = checked((short)reader.GetInt64(ordinal));
                    return true;
                }
                if (reader.GetFieldType(ordinal) != typeof(short)) return false;
                ((Row)instance).Value = reader.GetInt16(ordinal);
                return true;
            });
        if (ObjectConversion() != short.MaxValue || TypedAssignment() != short.MaxValue)
            throw new InvalidOperationException("Integer reader checksum failed.");
    }

    [Benchmark(Baseline = true)]
    public short ObjectConversion()
    {
        var raw = _reader.GetValue(0);
        var value = typeof(short).IsInstanceOfType(raw) ? raw :
            Convert.ChangeType(raw, typeof(short), CultureInfo.InvariantCulture);
        _member.GeneratedSetter(_row, value);
        return _row.Value;
    }

    [Benchmark]
    public short TypedAssignment()
    {
        if (!_member.GeneratedReaderSetter(_row, _reader, 0))
            throw new InvalidOperationException("Unexpected reader storage type.");
        return _row.Value;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _reader?.Dispose();
        _command?.Dispose();
        _connection?.Dispose();
    }

    public sealed class Row { public short Value { get; set; } }
}
