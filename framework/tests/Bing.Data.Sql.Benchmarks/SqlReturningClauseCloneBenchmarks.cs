using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// 测量 Returning 投影列数量增加时克隆子句的分配成本。
/// </summary>
[MemoryDiagnoser]
public class SqlReturningClauseCloneBenchmarks
{
    private ReturningClause _clause;
    private SqlMutationContext _cloneContext;

    /// <summary>
    /// 获取或设置 Returning 投影列数量。
    /// </summary>
    [Params(4, 32, 128)]
    public int ColumnCount { get; set; }

    /// <summary>
    /// 初始化列投影并核对克隆前后的完整 SQL。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var provider = SqliteSqlProvider.Instance;
        _clause = new ReturningClause(CreateContext(provider));
        _clause.AddRange(Enumerable.Range(0, ColumnCount)
            .Select(index => new SqlReturningColumn($"column_{index:D3}", "t", $"Column{index:D3}"))
            .ToArray());
        _clause.Validate(new SqlValidationContext(provider, 0, false, SqlExecutionKind.Update));

        var expectedSql = " Returning " + string.Join(", ", Enumerable.Range(0, ColumnCount)
            .Select(index => $"`t`.`column_{index:D3}` As `Column{index:D3}`"));
        if (ToSql(_clause) != expectedSql)
            throw new InvalidOperationException("Returning 克隆基准的来源子句未生成预期完整 SQL。");

        _cloneContext = CreateContext(provider);
        var clone = _clause.Clone(_cloneContext);
        clone.Validate(new SqlValidationContext(provider, 0, false, SqlExecutionKind.Update));
        if (ToSql(clone) != expectedSql)
            throw new InvalidOperationException("Returning 克隆基准的克隆子句未生成预期完整 SQL。");
    }

    /// <summary>
    /// 测量对现有 Returning 子句调用 Clone。
    /// </summary>
    [Benchmark]
    public IReturningClause CloneReturningClause() => _clause.Clone(_cloneContext);

    private static SqlMutationContext CreateContext(ISqlProvider provider)
    {
        IParameterManager parameterManager = provider.ParameterManagerFactory.Create(provider.Dialect);
        return new SqlMutationContext(provider, parameterManager, SqlBuilderServices.CreateDefault(),
            new SqlBuilderExecutionContext(null));
    }

    private static string ToSql(ISqlContent content)
    {
        var builder = new StringBuilder();
        content.AppendTo(builder);
        return builder.ToString();
    }
}
