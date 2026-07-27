using System.Linq.Expressions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Internal;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// Select子句
/// </summary>
public class SelectClause : ISelectClause
{
    /// <summary>
    /// 子句运行上下文。
    /// </summary>
    private readonly SqlClauseContext _context;

    /// <summary>
    /// SQL 生成器。
    /// </summary>
    private ISqlBuilder _sqlBuilder => _context.Builder;

    /// <summary>
    /// SQL 方言。
    /// </summary>
    private IDialect _dialect => _context.Dialect;

    /// <summary>
    /// 实体解析器。
    /// </summary>
    private IEntityResolver _resolver => _context.EntityResolver;

    /// <summary>
    /// 实体别名注册器。
    /// </summary>
    private IEntityAliasRegister _register => _context.AliasRegister;

    /// <summary>
    /// 列集合
    /// </summary>
    private readonly ColumnCollection _columns;

    /// <summary>
    /// 是否排除重复记录
    /// </summary>
    private bool _distinct;

    /// <summary>
    /// 初始化一个<see cref="SelectClause"/>类型的实例
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    public SelectClause(SqlClauseContext context)
        : this(context, null, false)
    {
    }

    /// <summary>
    /// 使用运行上下文和克隆状态初始化 Select 子句。
    /// </summary>
    protected SelectClause(SqlClauseContext context, ColumnCollection columns, bool distinct)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _columns = columns ?? new ColumnCollection();
        _distinct = distinct;
    }

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <returns>独立的 Select 子句。</returns>
    public virtual ISelectClause Clone(SqlClauseContext context) =>
        new SelectClause(context, _columns.Clone(), _distinct);

    /// <summary>
    /// 过滤重复记录
    /// </summary>
    public void Distinct() => _distinct = true;

    /// <inheritdoc />
    public void Count(string columnAlias = null) => CountAll(columnAlias);

    /// <inheritdoc />
    public void CountAll(string columnAlias = null) => _columns.AddAggregationColumn(SqlAggregateFunction.Count,
        null, columnAlias, wildcard: true);

    /// <inheritdoc />
    public void CountColumn(string column, string columnAlias = null, bool distinct = false) =>
        Aggregate(SqlAggregateFunction.Count, column, columnAlias, distinct);

    /// <summary>
    /// 求总行数
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Count(string column, string columnAlias, bool distinct = false) =>
        AggregateLegacy(SqlAggregateFunction.Count, column, columnAlias, distinct);

    /// <summary>
    /// 求总行数
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Count<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class =>
        AggregateLegacy(SqlAggregateFunction.Count, expression, columnAlias, distinct);

    /// <summary>
    /// 添加结构化聚合列。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="column">列名。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Aggregate(SqlAggregateFunction function, string column, string columnAlias = null,
        bool distinct = false)
    {
        SqlAggregateArgumentValidator.ValidateFunction(function);
        if (SqlAggregateArgumentValidator.ValidateWildcard(function, column, distinct, nameof(column)))
        {
            _columns.AddAggregationColumn(function, null, columnAlias, distinct, wildcard: true);
            return;
        }
        _columns.AddStructuredAggregationColumn(function, SqlAggregateArgumentValidator.ParseStructuredColumn(column),
            columnAlias, distinct, useDefaultAlias: false);
    }

    /// <summary>
    /// 添加实体表达式聚合列。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="function">聚合函数。</param>
    /// <param name="expression">列名表达式。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Aggregate<TEntity>(SqlAggregateFunction function, Expression<Func<TEntity, object>> expression,
        string columnAlias = null, bool distinct = false) where TEntity : class
    {
        SqlAggregateArgumentValidator.ValidateFunction(function);
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        _columns.AddStructuredAggregationColumn(function,
            SqlAggregateArgumentValidator.ParseStructuredColumn(_resolver.GetColumn(expression)), columnAlias,
            distinct, typeof(TEntity), useDefaultAlias: false);
    }

    /// <summary>
    /// 添加原始聚合参数。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="argumentSql">聚合参数 SQL。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void AggregateRaw(SqlAggregateFunction function, string argumentSql, string columnAlias = null,
        bool distinct = false)
    {
        SqlAggregateArgumentValidator.ValidateFunction(function);
        argumentSql = SqlAggregateArgumentValidator.ValidateExpression(argumentSql, nameof(argumentSql));
        SqlAggregateArgumentValidator.ValidateWildcard(function, argumentSql, distinct, nameof(argumentSql));
        _columns.AddAggregationColumn(function, argumentSql, columnAlias, distinct, argumentRaw: true);
    }

    /// <inheritdoc />
    public void AggregateExpression(SqlAggregateFunction function, string expressionSql, string columnAlias = null,
        bool distinct = false)
    {
        SqlAggregateArgumentValidator.ValidateFunction(function);
        expressionSql = SqlAggregateArgumentValidator.ValidateExpression(expressionSql, nameof(expressionSql));
        SqlAggregateArgumentValidator.ValidateWildcard(function, expressionSql, distinct, nameof(expressionSql));
        _columns.AddAggregationColumn(function, SqlExpressionIdentifierResolver.Resolve(expressionSql, _dialect), columnAlias, distinct,
            argumentRaw: true);
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Sum(string column, string columnAlias = null, bool distinct = false) =>
        AggregateLegacy(SqlAggregateFunction.Sum, column, columnAlias, distinct);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Sum<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class =>
        AggregateLegacy(SqlAggregateFunction.Sum, expression, columnAlias, distinct);

    /// <summary>
    /// 求平均值
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Avg(string column, string columnAlias = null, bool distinct = false) =>
        AggregateLegacy(SqlAggregateFunction.Avg, column, columnAlias, distinct);

    /// <summary>
    /// 求平均值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Avg<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class =>
        AggregateLegacy(SqlAggregateFunction.Avg, expression, columnAlias, distinct);

    /// <summary>
    /// 求最大值
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Max(string column, string columnAlias = null, bool distinct = false) =>
        AggregateLegacy(SqlAggregateFunction.Max, column, columnAlias, distinct);

    /// <summary>
    /// 求最大值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Max<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class =>
        AggregateLegacy(SqlAggregateFunction.Max, expression, columnAlias, distinct);

    /// <summary>
    /// 求最小值
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Min(string column, string columnAlias = null, bool distinct = false) =>
        AggregateLegacy(SqlAggregateFunction.Min, column, columnAlias, distinct);

    /// <summary>
    /// 求最小值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Min<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class =>
        AggregateLegacy(SqlAggregateFunction.Min, expression, columnAlias, distinct);

    /// <summary>
    /// 添加保留自动叶子列 Alias 的旧便捷结构化聚合列。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="column">列名。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    private void AggregateLegacy(SqlAggregateFunction function, string column, string columnAlias, bool distinct)
    {
        SqlAggregateArgumentValidator.ValidateFunction(function);
        if (SqlAggregateArgumentValidator.ValidateWildcard(function, column, distinct, nameof(column)))
        {
            _columns.AddAggregationColumn(function, null, columnAlias, distinct, wildcard: true);
            return;
        }
        _columns.AddStructuredAggregationColumn(function, SqlAggregateArgumentValidator.ParseStructuredColumn(column),
            columnAlias, distinct, useDefaultAlias: true);
    }

    /// <summary>
    /// 添加保留自动叶子列 Alias 的旧便捷实体表达式聚合列。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="function">聚合函数。</param>
    /// <param name="expression">列名表达式。</param>
    /// <param name="columnAlias">聚合结果列别名。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    private void AggregateLegacy<TEntity>(SqlAggregateFunction function, Expression<Func<TEntity, object>> expression,
        string columnAlias, bool distinct) where TEntity : class
    {
        SqlAggregateArgumentValidator.ValidateFunction(function);
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        _columns.AddStructuredAggregationColumn(function,
            SqlAggregateArgumentValidator.ParseStructuredColumn(_resolver.GetColumn(expression)), columnAlias,
            distinct, typeof(TEntity), useDefaultAlias: true);
    }

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <param name="columns">列名</param>
    /// <param name="tableAlias">表别名</param>
    public void Select(string columns, string tableAlias = null) => _columns.AddColumns(columns, tableAlias);

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    public void Select<TEntity>(bool propertyAsAlias = false) => _columns.AddColumns(_resolver.GetColumns<TEntity>(propertyAsAlias), typeof(TEntity));

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    public void Select<TEntity>(Expression<Func<TEntity, object[]>> expression, bool propertyAsAlias = false) where TEntity : class
    {
        if (expression == null)
            return;
        _columns.AddColumns(_resolver.GetColumns(expression, propertyAsAlias), tableType: typeof(TEntity));
    }

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    public void Select<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null) where TEntity : class
    {
        if (expression == null)
            return;
        _columns.AddColumns(_resolver.GetColumn(expression), typeof(TEntity), columnAlias);
    }

    /// <summary>
    /// 设置子查询列
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="columnAlias">列别名</param>
    public void Select(ISqlBuilder builder, string columnAlias)
    {
        if (builder == null)
            return;
        var result = _sqlBuilder is SqlBuilderBase sqlBuilder ? sqlBuilder.RenderSubquery(builder) : builder.ToSql();
        if (string.IsNullOrWhiteSpace(columnAlias) == false)
            result = $"({result})";
        AppendSql(result, columnAlias);
    }

    /// <summary>
    /// 设置子查询列
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="columnAlias">列别名</param>
    public void Select(Action<ISqlBuilder> action, string columnAlias)
    {
        if (action == null)
            return;
        var builder = _sqlBuilder.New();
        action(builder);
        Select(builder, columnAlias);
    }

    /// <summary>
    /// 添加到Select子句
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="columnAlias">列别名</param>
    public void AppendSql(string sql, string columnAlias = null)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        sql = Helper.ResolveSql(sql, _dialect);
        _columns.AddRawColumn(sql, columnAlias);
    }

    /// <summary>
    /// 移除列名
    /// </summary>
    /// <param name="columns">列名</param>
    /// <param name="tableAlias">表别名</param>
    public void RemoveSelect(string columns, string tableAlias = null) => _columns.RemoveColumns(columns, tableAlias);

    /// <summary>
    /// 移除列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    public void RemoveSelect<TEntity>(Expression<Func<TEntity, object[]>> expression) where TEntity : class
    {
        if (expression == null)
            return;
        _columns.RemoveColumns(_resolver.GetColumns(expression, false), typeof(TEntity));
    }

    /// <summary>
    /// 移除列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    public void RemoveSelect<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class
    {
        if (expression == null)
            return;
        _columns.RemoveColumns(_resolver.GetColumn(expression), typeof(TEntity));
    }

    /// <summary>
    /// 输出Sql
    /// </summary>
    public string ToSql() => $"Select {GetDistinct()}{GetColumns()}";

    /// <summary>
    /// 获取Distinct
    /// </summary>
    private string GetDistinct() => _distinct ? "Distinct " : null;

    /// <summary>
    /// 获取列名
    /// </summary>
    protected virtual string GetColumns() => _columns.Count == 0 ? "*" : _columns.ToSql(_dialect, _register);
}