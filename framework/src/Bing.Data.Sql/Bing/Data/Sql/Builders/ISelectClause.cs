using System.Linq.Expressions;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Select子句
/// </summary>
public interface ISelectClause
{
    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="context">重绑定后的子句运行上下文。</param>
    ISelectClause Clone(Core.SqlClauseContext context);

    /// <summary>
    /// 过滤重复记录
    /// </summary>
    void Distinct();

    /// <summary>
    /// 统计全部记录。
    /// </summary>
    /// <param name="columnAlias">聚合结果列别名。该重载始终表示 Count(*)，不表示待统计列。</param>
    void Count(string columnAlias = null);

    /// <summary>
    /// 统计全部记录。
    /// </summary>
    /// <param name="columnAlias">聚合结果列别名；未提供时不输出 Alias。</param>
    void CountAll(string columnAlias = null);

    /// <summary>
    /// 统计指定列的非空值。
    /// </summary>
    /// <param name="column">单个结构化列名。</param>
    /// <param name="columnAlias">聚合结果列别名；未提供时不输出 Alias。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void CountColumn(string column, string columnAlias = null, bool distinct = false);

    /// <summary>
    /// 求指定列的非空值数量；为兼容旧 API，未提供 Alias 时使用列路径的叶子名称。
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名；未提供时使用列路径的叶子名称。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void Count(string column, string columnAlias, bool distinct = false);

    /// <summary>
    /// 求总行数
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void Count<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null, bool distinct = false)
        where TEntity : class;

    /// <summary>
    /// 添加结构化聚合列。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="column">单个结构化列名，不支持表达式、函数或多个列。</param>
    /// <param name="columnAlias">聚合结果列别名；未提供时不输出 Alias。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void Aggregate(SqlAggregateFunction function, string column, string columnAlias = null, bool distinct = false);

    /// <summary>
    /// 添加实体表达式聚合列。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="function">聚合函数。</param>
    /// <param name="expression">列名表达式。</param>
    /// <param name="columnAlias">聚合结果列别名；未提供时不输出 Alias。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void Aggregate<TEntity>(SqlAggregateFunction function, Expression<Func<TEntity, object>> expression,
        string columnAlias = null, bool distinct = false) where TEntity : class;

    /// <summary>
    /// 添加完全原样的聚合参数。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="argumentSql">受信任的原始聚合参数 SQL。不解析、不校验标识符，也不转换 []；参数必须通过 AddParam 显式提供。</param>
    /// <param name="columnAlias">聚合结果列别名；未提供时不输出 Alias。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void AggregateRaw(SqlAggregateFunction function, string argumentSql, string columnAlias = null,
        bool distinct = false);

    /// <summary>
    /// 添加包含方括号标识符占位符的聚合表达式。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="expressionSql">聚合表达式 SQL，仅普通 SQL 上下文中的 [] 会按当前方言转换为标识符引用符；字符串和注释原文保持不变，参数必须通过 AddParam 显式提供。</param>
    /// <param name="columnAlias">聚合结果列别名；未提供时不输出 Alias。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void AggregateExpression(SqlAggregateFunction function, string expressionSql, string columnAlias = null,
        bool distinct = false);

    /// <summary>
    /// 求和；为兼容旧 API，未提供 Alias 时使用列路径的叶子名称。
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名；未提供时使用列路径的叶子名称。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void Sum(string column, string columnAlias = null, bool distinct = false);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void Sum<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null, bool distinct = false)
        where TEntity : class;

    /// <summary>
    /// 求平均值；为兼容旧 API，未提供 Alias 时使用列路径的叶子名称。
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名；未提供时使用列路径的叶子名称。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void Avg(string column, string columnAlias = null, bool distinct = false);

    /// <summary>
    /// 求平均值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void Avg<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null, bool distinct = false)
        where TEntity : class;

    /// <summary>
    /// 求最大值；为兼容旧 API，未提供 Alias 时使用列路径的叶子名称。
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名；未提供时使用列路径的叶子名称。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void Max(string column, string columnAlias = null, bool distinct = false);

    /// <summary>
    /// 求最大值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void Max<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null, bool distinct = false)
        where TEntity : class;

    /// <summary>
    /// 求最小值；为兼容旧 API，未提供 Alias 时使用列路径的叶子名称。
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名；未提供时使用列路径的叶子名称。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void Min(string column, string columnAlias = null, bool distinct = false);

    /// <summary>
    /// 求最小值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    void Min<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null, bool distinct = false)
        where TEntity : class;

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <param name="columns">列名</param>
    /// <param name="tableAlias">表别名</param>
    void Select(string columns, string tableAlias = null);

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    void Select<TEntity>(bool propertyAsAlias = false);

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    void Select<TEntity>(Expression<Func<TEntity, object[]>> expression, bool propertyAsAlias = false)
        where TEntity : class;

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    void Select<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null) where TEntity : class;

    /// <summary>
    /// 设置子查询列
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="columnAlias">列别名</param>
    void Select(ISqlBuilder builder, string columnAlias);

    /// <summary>
    /// 设置子查询列
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="columnAlias">列别名</param>
    void Select(Action<ISqlBuilder> action, string columnAlias);

    /// <summary>
    /// 添加到Select子句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="columnAlias">列别名</param>
    void AppendSql(string sql, string columnAlias = null);

    /// <summary>
    /// 移除列名
    /// </summary>
    /// <param name="columns">列名</param>
    /// <param name="tableAlias">表别名</param>
    void RemoveSelect(string columns, string tableAlias = null);

    /// <summary>
    /// 移除列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    void RemoveSelect<TEntity>(Expression<Func<TEntity, object[]>> expression) where TEntity : class;

    /// <summary>
    /// 移除列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    void RemoveSelect<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

    /// <summary>
    /// 输出Sql
    /// </summary>
    string ToSql();
}