using System;
using Bing.Data.Sql.Builders.Extensions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 列
/// </summary>
public class ColumnItem
{
    /// <summary>
    /// 列名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 表别名
    /// </summary>
    public string TableAlias { get; set; }

    /// <summary>
    /// 列别名
    /// </summary>
    public string ColumnAlias { get; set; }

    /// <summary>
    /// 是否使用原始值
    /// </summary>
    public bool Raw { get; }

    /// <summary>
    /// 表实体类型
    /// </summary>
    public Type TableType { get; }

    /// <summary>
    /// 是否聚合函数
    /// </summary>
    public bool IsAggregation { get; }

    /// <summary>
    /// 聚合函数
    /// </summary>
    public string AggregationFunc { get; }

    /// <summary>
    /// 初始化一个<see cref="ColumnItem"/>类型的实例
    /// </summary>
    /// <param name="name">列名</param>
    /// <param name="tableAlias">表别名</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="tableType">表类型</param>
    /// <param name="raw">是否使用原始值</param>
    /// <param name="isAggregation">是否聚合函数</param>
    /// <param name="aggregationFunc">聚合函数</param>
    public ColumnItem(string name, string tableAlias = null, string columnAlias = null, Type tableType = null, bool raw = false, bool isAggregation = false, string aggregationFunc = null)
    {
        Name = name;
        TableAlias = tableAlias;
        ColumnAlias = columnAlias;
        TableType = tableType;
        Raw = raw;
        IsAggregation = isAggregation;
        AggregationFunc = aggregationFunc;
    }

    /// <summary>
    /// 获取列名列表
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="register">实体别名注册器</param>
    public string ToSql(IDialect dialect, IEntityAliasRegister register)
    {
        if (Raw || IsAggregation && TableType == null && string.IsNullOrWhiteSpace(AggregationFunc))
            return dialect.GetColumn(Name, dialect.GetSafeName(ColumnAlias));
        var result = new SqlItem(Name, GetTableAlias(register), ColumnAlias, isResolve: false, aggregationFunc: AggregationFunc);
        return result.ToSql(dialect);
    }

    /// <summary>
    /// 获取表别名
    /// </summary>
    /// <param name="register">实体别名注册器</param>
    private string GetTableAlias(IEntityAliasRegister register) => register != null && register.Contains(TableType) ? register.GetAlias(TableType) : TableAlias;

    /// <summary>
    /// 克隆
    /// </summary>
    public ColumnItem Clone() => new ColumnItem(Name, TableAlias, ColumnAlias, TableType, Raw, IsAggregation);
}