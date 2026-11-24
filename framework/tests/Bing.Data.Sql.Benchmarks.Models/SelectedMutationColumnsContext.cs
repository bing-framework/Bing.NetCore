using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Generated;

namespace Bing.Data.Sql.Benchmarks.Models;

// 基准独立生成模型，避免对已有运行时 Lambda 基准启用查询拦截器。
/// <summary>用于比较筛选列 SQL 路径的生成实体。</summary>
[Table("selected_mutation_rows")]
public sealed class SelectedMutationColumnsRow
{
    /// <summary>数据库生成主键。</summary>
    [Key, Column("row_id"), DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>显示名称。</summary>
    [Column("display_name")]
    public string Name { get; set; }

    /// <summary>单价。</summary>
    [Column("unit_price")]
    public decimal Price { get; set; }

    /// <summary>库存编码。</summary>
    [Column("stock_code")]
    public string Code { get; set; }

    /// <summary>未选择的备注。</summary>
    [Column("notes")]
    public string Notes { get; set; }

    /// <summary>并发版本。</summary>
    [ConcurrencyCheck, Column("row_version")]
    public int Version { get; set; }

    /// <summary>数据库计算列。</summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Computed), Column("calculated_value")]
    public int Calculated { get; set; }
}

/// <summary>在编译基准夹具时生成 SQL、Getter 和结果读取器。</summary>
[SqlModel(typeof(SelectedMutationColumnsRow))]
public static partial class SelectedMutationColumnsContext { }
