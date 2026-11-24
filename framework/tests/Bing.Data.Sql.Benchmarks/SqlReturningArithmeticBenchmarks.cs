using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>测量 Returning 二元/一元算术列构造与映射后方言渲染分配。</summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 6, iterationCount: 15, id: "FormalHost")]
public class SqlReturningArithmeticBenchmarks
{
    private readonly string[] _binaryParts = { "(", "physical_amount", " + ", "physical_adjustment", ")" };
    private readonly string[] _unaryParts = { "(-", "physical_amount", ")" };
    private readonly string[] _mappedParts = { "(-", "physical_amount", ") + ", "physical_adjustment", "" };
    private readonly StringBuilder _buffer = new();
    private SqlReturningColumn _mappedColumn;

    /// <summary>初始化结构片段，并校验绑定物理列后的完整 SQL。</summary>
    [GlobalSetup]
    public void Setup()
    {
        var binary = SqlReturningColumn.CreateComputed(_binaryParts, "t", "Total");
        var binarySql = new StringBuilder();
        binary.AppendTo(binarySql, SqliteSqlProvider.Instance.Dialect);
        if (binarySql.ToString() != "(`t`.`physical_amount` + `t`.`physical_adjustment`) As `Total`")
            throw new InvalidOperationException("Returning 二元算术基准 SQL 不符合预期。");

        var unarySql = new StringBuilder();
        SqlReturningColumn.CreateArithmetic(_unaryParts, "t", "NegativeAmount")
            .AppendTo(unarySql, SqliteSqlProvider.Instance.Dialect);
        if (unarySql.ToString() != "(-`t`.`physical_amount`) As `NegativeAmount`")
            throw new InvalidOperationException("Returning 一元算术基准 SQL 不符合预期。");

        // 映射已把实体属性名替换为物理列名；AppendTo 仅按当前 Provider 格式化列。
        _mappedColumn = SqlReturningColumn.CreateArithmetic(_mappedParts, alias: "NetAmount");
        _mappedColumn.AppendTo(_buffer, SqlServerSqlProvider.Instance.Dialect, "Inserted");
        if (_buffer.ToString() != "(-[Inserted].[physical_amount]) + [Inserted].[physical_adjustment] As [NetAmount]")
            throw new InvalidOperationException("Returning 映射后方言 SQL 不符合预期。");
        _buffer.Clear();
    }

    /// <summary>测量已有二元计算列合同的创建成本。</summary>
    [Benchmark]
    public SqlReturningColumn CreateBinaryComputed() =>
        SqlReturningColumn.CreateComputed(_binaryParts, "t", "Total");

    /// <summary>测量生成器专用一元算术列合同的创建成本。</summary>
    [Benchmark]
    public SqlReturningColumn CreateUnaryArithmetic() =>
        SqlReturningColumn.CreateArithmetic(_unaryParts, "t", "NegativeAmount");

    /// <summary>测量已绑定物理列的计算投影按 SQL Server 方言追加成本。</summary>
    [Benchmark]
    public string AppendMappedArithmetic()
    {
        _buffer.Clear();
        _mappedColumn.AppendTo(_buffer, SqlServerSqlProvider.Instance.Dialect, "Inserted");
        return _buffer.ToString();
    }
}
