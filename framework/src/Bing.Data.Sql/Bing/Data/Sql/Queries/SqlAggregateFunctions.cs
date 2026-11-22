using System;
using System.Linq.Expressions;

namespace Bing.Data.Sql;

/// <summary>
/// 为 SQL 聚合投影提供强类型表达式标记。
/// </summary>
/// <typeparam name="TEntity">聚合所属的实体类型。</typeparam>
public sealed class SqlAggregateFunctions<TEntity>
{
    /// <summary>
    /// 定义在 CLR 中直接调用聚合标记方法时抛出的错误消息。
    /// </summary>
    private const string NotSupportedMessage = "聚合标记方法只能用于 SQL 表达式，不能在 CLR 中直接调用。";

    /// <summary>
    /// 初始化一个 <see cref="SqlAggregateFunctions{TEntity}" /> 类型的实例。
    /// </summary>
    internal SqlAggregateFunctions()
    {
    }

    /// <summary>
    /// 声明 COUNT(*) 聚合表达式。
    /// </summary>
    /// <returns>在 SQL 表达式树中表示的 COUNT(*) 结果；不得直接调用。</returns>
    /// <remarks>
    /// 仅供构造 SQL 聚合表达式使用，直接执行会抛出 NotSupportedException。
    /// </remarks>
    public long CountAll() =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 声明 COUNT(*) 聚合表达式。
    /// </summary>
    /// <param name="columnAlias">COUNT(*) 结果列别名。</param>
    /// <returns>在 SQL 表达式树中表示的 COUNT(*) 结果；不得直接调用。</returns>
    /// <remarks>
    /// 仅供构造 SQL 聚合表达式使用，直接执行会抛出 NotSupportedException。
    /// </remarks>
    public long CountAll(string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计列非空值的 Count 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>Count 聚合结果。</returns>
    public long Count<TValue>(Expression<Func<TEntity, TValue>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计列非空值的 Count 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>Count 聚合结果。</returns>
    public long Count<TValue>(Expression<Func<TEntity, TValue>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计列非空值的 Count 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Count 聚合结果。</returns>
    public long Count<TValue>(Expression<Func<TEntity, TValue>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计列非空值的 Count 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Count 聚合结果。</returns>
    public long Count<TValue>(Expression<Func<TEntity, TValue>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public int? Sum(Expression<Func<TEntity, int>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public int? Sum(Expression<Func<TEntity, int>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public int? Sum(Expression<Func<TEntity, int>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public int? Sum(Expression<Func<TEntity, int>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public int? Sum(Expression<Func<TEntity, int?>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public int? Sum(Expression<Func<TEntity, int?>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public int? Sum(Expression<Func<TEntity, int?>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public int? Sum(Expression<Func<TEntity, int?>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public long? Sum(Expression<Func<TEntity, long>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public long? Sum(Expression<Func<TEntity, long>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public long? Sum(Expression<Func<TEntity, long>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public long? Sum(Expression<Func<TEntity, long>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public long? Sum(Expression<Func<TEntity, long?>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public long? Sum(Expression<Func<TEntity, long?>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public long? Sum(Expression<Func<TEntity, long?>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public long? Sum(Expression<Func<TEntity, long?>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public decimal? Sum(Expression<Func<TEntity, decimal>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public decimal? Sum(Expression<Func<TEntity, decimal>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public decimal? Sum(Expression<Func<TEntity, decimal>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public decimal? Sum(Expression<Func<TEntity, decimal>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public decimal? Sum(Expression<Func<TEntity, decimal?>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public decimal? Sum(Expression<Func<TEntity, decimal?>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public decimal? Sum(Expression<Func<TEntity, decimal?>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public decimal? Sum(Expression<Func<TEntity, decimal?>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public double? Sum(Expression<Func<TEntity, double>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public double? Sum(Expression<Func<TEntity, double>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public double? Sum(Expression<Func<TEntity, double>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public double? Sum(Expression<Func<TEntity, double>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public double? Sum(Expression<Func<TEntity, double?>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public double? Sum(Expression<Func<TEntity, double?>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public double? Sum(Expression<Func<TEntity, double?>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    public double? Sum(Expression<Func<TEntity, double?>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public decimal? Avg(Expression<Func<TEntity, decimal>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public decimal? Avg(Expression<Func<TEntity, decimal>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public decimal? Avg(Expression<Func<TEntity, decimal>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public decimal? Avg(Expression<Func<TEntity, decimal>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public decimal? Avg(Expression<Func<TEntity, decimal?>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public decimal? Avg(Expression<Func<TEntity, decimal?>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public decimal? Avg(Expression<Func<TEntity, decimal?>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public decimal? Avg(Expression<Func<TEntity, decimal?>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public double? Avg(Expression<Func<TEntity, double>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public double? Avg(Expression<Func<TEntity, double>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public double? Avg(Expression<Func<TEntity, double>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public double? Avg(Expression<Func<TEntity, double>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public double? Avg(Expression<Func<TEntity, double?>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public double? Avg(Expression<Func<TEntity, double?>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public double? Avg(Expression<Func<TEntity, double?>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    public double? Avg(Expression<Func<TEntity, double?>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    public TValue? Min<TValue>(Expression<Func<TEntity, TValue>> column) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    public TValue? Min<TValue>(Expression<Func<TEntity, TValue>> column, string columnAlias) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    public TValue? Min<TValue>(Expression<Func<TEntity, TValue>> column, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    public TValue? Min<TValue>(Expression<Func<TEntity, TValue>> column, string columnAlias, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    public TValue? Min<TValue>(Expression<Func<TEntity, TValue?>> column) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    public TValue? Min<TValue>(Expression<Func<TEntity, TValue?>> column, string columnAlias) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    public TValue? Min<TValue>(Expression<Func<TEntity, TValue?>> column, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    public TValue? Min<TValue>(Expression<Func<TEntity, TValue?>> column, string columnAlias, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>Min 聚合结果。</returns>
    public string Min(Expression<Func<TEntity, string>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>Min 聚合结果。</returns>
    public string Min(Expression<Func<TEntity, string>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Min 聚合结果。</returns>
    public string Min(Expression<Func<TEntity, string>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Min 聚合结果。</returns>
    public string Min(Expression<Func<TEntity, string>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    public TValue? Max<TValue>(Expression<Func<TEntity, TValue>> column) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    public TValue? Max<TValue>(Expression<Func<TEntity, TValue>> column, string columnAlias) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    public TValue? Max<TValue>(Expression<Func<TEntity, TValue>> column, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    public TValue? Max<TValue>(Expression<Func<TEntity, TValue>> column, string columnAlias, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    public TValue? Max<TValue>(Expression<Func<TEntity, TValue?>> column) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    public TValue? Max<TValue>(Expression<Func<TEntity, TValue?>> column, string columnAlias) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    public TValue? Max<TValue>(Expression<Func<TEntity, TValue?>> column, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">选择器结果的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    public TValue? Max<TValue>(Expression<Func<TEntity, TValue?>> column, string columnAlias, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <returns>Max 聚合结果。</returns>
    public string Max(Expression<Func<TEntity, string>> column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>Max 聚合结果。</returns>
    public string Max(Expression<Func<TEntity, string>> column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Max 聚合结果。</returns>
    public string Max(Expression<Func<TEntity, string>> column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的列选择器。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Max 聚合结果。</returns>
    public string Max(Expression<Func<TEntity, string>> column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计列非空值的 Count 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>Count 聚合结果。</returns>
    /// <remarks>
    /// 创建统计成员列非空值的 Count 表达式标记。
    /// </remarks>
    public long Count<TValue>(TValue column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计列非空值的 Count 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>Count 聚合结果。</returns>
    /// <remarks>
    /// 创建统计成员列非空值的 Count 表达式标记。
    /// </remarks>
    public long Count<TValue>(TValue column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计列非空值的 Count 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Count 聚合结果。</returns>
    /// <remarks>
    /// 创建统计成员列非空值的 Count 表达式标记。
    /// </remarks>
    public long Count<TValue>(TValue column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计列非空值的 Count 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Count 聚合结果。</returns>
    /// <remarks>
    /// 创建统计成员列非空值的 Count 表达式标记。
    /// </remarks>
    public long Count<TValue>(TValue column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public int? Sum(int column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public int? Sum(int column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public int? Sum(int column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public int? Sum(int column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public int? Sum(int? column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public int? Sum(int? column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public int? Sum(int? column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public int? Sum(int? column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public long? Sum(long column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public long? Sum(long column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public long? Sum(long column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public long? Sum(long column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public long? Sum(long? column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public long? Sum(long? column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public long? Sum(long? column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public long? Sum(long? column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public decimal? Sum(decimal column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public decimal? Sum(decimal column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public decimal? Sum(decimal column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public decimal? Sum(decimal column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public decimal? Sum(decimal? column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public decimal? Sum(decimal? column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public decimal? Sum(decimal? column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public decimal? Sum(decimal? column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public double? Sum(double column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public double? Sum(double column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public double? Sum(double column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public double? Sum(double column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public double? Sum(double? column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public double? Sum(double? column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public double? Sum(double? column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列之和的 Sum 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Sum 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列之和的 Sum 表达式标记。
    /// </remarks>
    public double? Sum(double? column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public decimal? Avg(decimal column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public decimal? Avg(decimal column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public decimal? Avg(decimal column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public decimal? Avg(decimal column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public decimal? Avg(decimal? column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public decimal? Avg(decimal? column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public decimal? Avg(decimal? column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public decimal? Avg(decimal? column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public double? Avg(double column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public double? Avg(double column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public double? Avg(double column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public double? Avg(double column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public double? Avg(double? column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public double? Avg(double? column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public double? Avg(double? column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值列平均值的 Avg 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Avg 聚合结果。</returns>
    /// <remarks>
    /// 计算数值成员列平均值的 Avg 表达式标记。
    /// </remarks>
    public double? Avg(double? column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public TValue? Min<TValue>(TValue column) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public TValue? Min<TValue>(TValue column, string columnAlias) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public TValue? Min<TValue>(TValue column, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public TValue? Min<TValue>(TValue column, string columnAlias, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public TValue? Min<TValue>(TValue? column) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public TValue? Min<TValue>(TValue? column, string columnAlias) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public TValue? Min<TValue>(TValue? column, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public TValue? Min<TValue>(TValue? column, string columnAlias, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <returns>Min 聚合结果。</returns>
    /// <remarks>
    /// 计算字符串成员列最小值的 Min 表达式标记。
    /// </remarks>
    public string Min(string column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>Min 聚合结果。</returns>
    /// <remarks>
    /// 计算字符串成员列最小值的 Min 表达式标记。
    /// </remarks>
    public string Min(string column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Min 聚合结果。</returns>
    /// <remarks>
    /// 计算字符串成员列最小值的 Min 表达式标记。
    /// </remarks>
    public string Min(string column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Min 聚合结果。</returns>
    /// <remarks>
    /// 计算字符串成员列最小值的 Min 表达式标记。
    /// </remarks>
    public string Min(string column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public TValue? Max<TValue>(TValue column) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public TValue? Max<TValue>(TValue column, string columnAlias) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public TValue? Max<TValue>(TValue column, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public TValue? Max<TValue>(TValue column, string columnAlias, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public TValue? Max<TValue>(TValue? column) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public TValue? Max<TValue>(TValue? column, string columnAlias) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public TValue? Max<TValue>(TValue? column, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public TValue? Max<TValue>(TValue? column, string columnAlias, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <returns>Max 聚合结果。</returns>
    /// <remarks>
    /// 计算字符串成员列最大值的 Max 表达式标记。
    /// </remarks>
    public string Max(string column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>Max 聚合结果。</returns>
    /// <remarks>
    /// 计算字符串成员列最大值的 Max 表达式标记。
    /// </remarks>
    public string Max(string column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Max 聚合结果。</returns>
    /// <remarks>
    /// 计算字符串成员列最大值的 Max 表达式标记。
    /// </remarks>
    public string Max(string column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Max 聚合结果。</returns>
    /// <remarks>
    /// 计算字符串成员列最大值的 Max 表达式标记。
    /// </remarks>
    public string Max(string column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);
}
