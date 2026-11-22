using System;

namespace Bing.Data.Sql;

/// <summary>
/// 为 SQL 聚合投影提供静态强类型表达式标记。
/// </summary>
/// <remarks>所有方法只能出现在 SQL 表达式中，不能在 CLR 中直接求值。</remarks>
public static class SqlFn
{
    /// <summary>
    /// 定义在 CLR 中直接调用 SQL 标记方法时抛出的错误消息。
    /// </summary>
    private const string NotSupportedMessage = "聚合标记方法只能用于 SQL 表达式，不能在 CLR 中直接调用。";

    /// <summary>
    /// 创建统计当前查询行数的 CountAll 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <returns>COUNT(*) 查询表达式对应的行数结果。</returns>
    public static long CountAll() =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计当前查询行数的 CountAll 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>COUNT(*) 查询表达式对应的行数结果。</returns>
    public static long CountAll(string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计成员列非空值的 Count 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <typeparam name="TValue">成员值的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>COUNT(column) 查询表达式对应的非空成员值数量；distinct 重载仅统计不同值。</returns>
    public static long Count<TValue>(TValue column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计成员列非空值的 Count 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <typeparam name="TValue">成员值的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>COUNT(column) 查询表达式对应的非空成员值数量；distinct 重载仅统计不同值。</returns>
    public static long Count<TValue>(TValue column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计成员列非空值的 Count 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <typeparam name="TValue">成员值的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>COUNT(column) 查询表达式对应的非空成员值数量；distinct 重载仅统计不同值。</returns>
    public static long Count<TValue>(TValue column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建统计成员列非空值的 Count 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <typeparam name="TValue">成员值的 CLR 类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>COUNT(column) 查询表达式对应的非空成员值数量；distinct 重载仅统计不同值。</returns>
    public static long Count<TValue>(TValue column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static int? Sum(int column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static int? Sum(int column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static int? Sum(int column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static int? Sum(int column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static int? Sum(int? column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static int? Sum(int? column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static int? Sum(int? column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static int? Sum(int? column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static long? Sum(long column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static long? Sum(long column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static long? Sum(long column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static long? Sum(long column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static long? Sum(long? column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static long? Sum(long? column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static long? Sum(long? column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static long? Sum(long? column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static decimal? Sum(decimal column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static decimal? Sum(decimal column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static decimal? Sum(decimal column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static decimal? Sum(decimal column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static decimal? Sum(decimal? column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static decimal? Sum(decimal? column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static decimal? Sum(decimal? column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static decimal? Sum(decimal? column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static double? Sum(double column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static double? Sum(double column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static double? Sum(double column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static double? Sum(double column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static double? Sum(double? column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static double? Sum(double? column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static double? Sum(double? column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列之和的 Sum 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 求和结果；没有非 null 输入值时为 null。</returns>
    public static double? Sum(double? column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static decimal? Avg(decimal column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static decimal? Avg(decimal column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static decimal? Avg(decimal column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static decimal? Avg(decimal column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static decimal? Avg(decimal? column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static decimal? Avg(decimal? column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static decimal? Avg(decimal? column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static decimal? Avg(decimal? column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static double? Avg(double column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static double? Avg(double column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static double? Avg(double column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static double? Avg(double column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static double? Avg(double? column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static double? Avg(double? column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static double? Avg(double? column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算数值成员列平均值的 Avg 表达式标记。
    /// </summary>
    /// <remarks>仅供 SQL 表达式解析，直接调用会抛出 NotSupportedException。</remarks>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>SQL 平均值；没有非 null 输入值时为 null。</returns>
    public static double? Avg(double? column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 创建计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public static TValue? Min<TValue>(TValue column) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 创建计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public static TValue? Min<TValue>(TValue column, string columnAlias) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 创建计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public static TValue? Min<TValue>(TValue column, bool distinct) where TValue : struct =>
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
    /// 创建计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public static TValue? Min<TValue>(TValue column, string columnAlias, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 创建计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public static TValue? Min<TValue>(TValue? column) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 创建计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public static TValue? Min<TValue>(TValue? column, string columnAlias) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Min 聚合结果。</returns>
    /// <remarks>
    /// 创建计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public static TValue? Min<TValue>(TValue? column, bool distinct) where TValue : struct =>
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
    /// 创建计算成员列最小值的 Min 表达式标记。
    /// </remarks>
    public static TValue? Min<TValue>(TValue? column, string columnAlias, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <returns>Min 聚合结果。</returns>
    /// <remarks>
    /// 创建计算字符串成员列最小值的 Min 表达式标记。
    /// </remarks>
    public static string Min(string column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>Min 聚合结果。</returns>
    /// <remarks>
    /// 创建计算字符串成员列最小值的 Min 表达式标记。
    /// </remarks>
    public static string Min(string column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Min 聚合结果。</returns>
    /// <remarks>
    /// 创建计算字符串成员列最小值的 Min 表达式标记。
    /// </remarks>
    public static string Min(string column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最小值的 Min 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Min 聚合结果。</returns>
    /// <remarks>
    /// 创建计算字符串成员列最小值的 Min 表达式标记。
    /// </remarks>
    public static string Min(string column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 创建计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public static TValue? Max<TValue>(TValue column) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 创建计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public static TValue? Max<TValue>(TValue column, string columnAlias) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 创建计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public static TValue? Max<TValue>(TValue column, bool distinct) where TValue : struct =>
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
    /// 创建计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public static TValue? Max<TValue>(TValue column, string columnAlias, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 创建计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public static TValue? Max<TValue>(TValue? column) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 创建计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public static TValue? Max<TValue>(TValue? column, string columnAlias) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <typeparam name="TValue">成员值的结构类型。</typeparam>
    /// <param name="column">参与聚合的实体成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>可空的 Max 聚合结果。</returns>
    /// <remarks>
    /// 创建计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public static TValue? Max<TValue>(TValue? column, bool distinct) where TValue : struct =>
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
    /// 创建计算成员列最大值的 Max 表达式标记。
    /// </remarks>
    public static TValue? Max<TValue>(TValue? column, string columnAlias, bool distinct) where TValue : struct =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <returns>Max 聚合结果。</returns>
    /// <remarks>
    /// 创建计算字符串成员列最大值的 Max 表达式标记。
    /// </remarks>
    public static string Max(string column) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <returns>Max 聚合结果。</returns>
    /// <remarks>
    /// 创建计算字符串成员列最大值的 Max 表达式标记。
    /// </remarks>
    public static string Max(string column, string columnAlias) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Max 聚合结果。</returns>
    /// <remarks>
    /// 创建计算字符串成员列最大值的 Max 表达式标记。
    /// </remarks>
    public static string Max(string column, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

    /// <summary>
    /// 创建计算列最大值的 Max 表达式标记。
    /// </summary>
    /// <param name="column">参与聚合的字符串成员值。</param>
    /// <param name="columnAlias">聚合结果列名。</param>
    /// <param name="distinct">是否先对列值去重。</param>
    /// <returns>Max 聚合结果。</returns>
    /// <remarks>
    /// 创建计算字符串成员列最大值的 Max 表达式标记。
    /// </remarks>
    public static string Max(string column, string columnAlias, bool distinct) =>
        throw new NotSupportedException(NotSupportedMessage);

}

