using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Generated;

namespace Bing.Data.Sql.Benchmarks.Models;

/// <summary>Returning SQL 投影基准实体。</summary>
[Table("returning_projection_benchmarks")]
public sealed class ReturningProjectionRow
{
    /// <summary>实体主键。</summary>
    [Column("row_id")]
    public int Id { get; set; }

    /// <summary>显示名称。</summary>
    [Column("display_name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>Returning DTO 投影基准结果。</summary>
public sealed class ReturningProjectionDto
{
    /// <summary>返回主键。</summary>
    public int Key { get; set; }

    /// <summary>返回显示名称。</summary>
    public string Display { get; set; } = string.Empty;
}

/// <summary>验证需要引用和转义的固定物理列名。</summary>
public sealed class QuotedReturningProjectionRow
{
    /// <summary>带空格的物理键列。</summary>
    [Column("row id")]
    public int Id { get; set; }

    /// <summary>带方言引用符的物理名称列。</summary>
    [Column("display`name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>在独立模型项目内生成 Returning 调用点。</summary>
[SqlModel(typeof(ReturningProjectionRow))]
[SqlModel(typeof(QuotedReturningProjectionRow))]
public static partial class ReturningProjectionContext
{
    private static readonly Expression<Func<ReturningProjectionRow, object>> CachedColumns =
        row => new { Key = row.Id, Display = row.Name };
    private static readonly Expression<Func<ReturningProjectionRow, object>> CachedArithmetic =
        row => new { Total = (row.Id + row.Id) * row.Id };

    /// <summary>复用表达式树，并由生成器直接提供列布局与 SQL。</summary>
    public static ISqlUpdateBuilder ApplyCachedAnonymous(ISqlUpdateBuilder source) =>
        source.Returning<ReturningProjectionRow>(CachedColumns);

    /// <summary>复用计算表达式树，避免每次请求构造相同投影。</summary>
    public static ISqlUpdateBuilder ApplyCachedComputedArithmetic(ISqlUpdateBuilder source) =>
        source.Returning<ReturningProjectionRow>(CachedArithmetic);

    /// <summary>通过生成调用点构造匿名对象投影。</summary>
    public static ISqlUpdateBuilder ApplyAnonymous(ISqlUpdateBuilder source) =>
        source.Returning<ReturningProjectionRow>(row => new { Key = row.Id, Display = row.Name });

    /// <summary>通过生成调用点构造 DTO 成员初始化投影。</summary>
    public static ISqlUpdateBuilder ApplyDto(ISqlUpdateBuilder source) =>
        source.Returning<ReturningProjectionRow>(row => new ReturningProjectionDto
        {
            Key = row.Id,
            Display = row.Name
        });

    /// <summary>通过生成调用点构造匿名数值算术投影。</summary>
    public static ISqlUpdateBuilder ApplyComputedArithmetic(ISqlUpdateBuilder source) =>
        source.Returning<ReturningProjectionRow>(row => new { Total = (row.Id + row.Id) * row.Id });

    /// <summary>通过相同 API 生成需要转义的直接列布局。</summary>
    public static ISqlUpdateBuilder ApplyQuotedColumns(ISqlUpdateBuilder source) =>
        source.Returning<QuotedReturningProjectionRow>(row => new { row.Id, row.Name });

    /// <summary>通过相同 API 生成需要转义的数值运算布局。</summary>
    public static ISqlUpdateBuilder ApplyQuotedArithmetic(ISqlUpdateBuilder source) =>
        source.Returning<QuotedReturningProjectionRow>(row => new { Total = -(row.Id + row.Id), row.Name });
}
