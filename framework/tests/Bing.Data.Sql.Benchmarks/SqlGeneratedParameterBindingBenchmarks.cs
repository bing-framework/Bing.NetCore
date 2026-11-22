using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Data.Sqlite;
using System.Linq;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>度量生成式标量与集合参数绑定的时间和分配；不包含数据库执行。</summary>
[MemoryDiagnoser]
public class SqlGeneratedParameterBindingBenchmarks
{
    private SqlParam[] _scalars;
    private SqlParam[] _collection;

    [Params(0, 2, 16, 128)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _scalars = new[] { new SqlParam("ids0", 2) };
        _collection = new[] { new SqlParam("ids", Enumerable.Range(0, Count).ToArray()), _scalars[0] };
    }

    [Benchmark(Baseline = true)]
    public int ScalarOnly()
    {
        using var command = new SqliteCommand { CommandText = "SELECT @ids0" };
        SqlGeneratedExecution.BindParameters(command, _scalars);
        return command.Parameters.Count;
    }

    [Benchmark]
    public int Collection()
    {
        using var command = new SqliteCommand { CommandText = "SELECT @ids, @ids0, '@ids' /* @ids */" };
        SqlGeneratedExecution.BindParameters(command, _collection);
        return command.Parameters.Count;
    }
}
