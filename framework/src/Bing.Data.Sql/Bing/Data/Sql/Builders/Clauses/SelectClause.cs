using System.Linq.Expressions;
using System.Text;
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

    /// <inheritdoc />
    public bool IsDistinct => _distinct;

    /// <summary>
    /// 当前投影数量是否可以可靠确定。
    /// </summary>
    private bool _projectionCountKnown = true;

    /// <inheritdoc />
    public int? ProjectionCount => _columns.Count == 0 || _projectionCountKnown == false ? null : _columns.Count;

    /// <summary>
    /// 是否包含结构化聚合投影。
    /// </summary>
    public bool HasAggregate
    {
        get
        {
            for (var index = 0; index < _columns.Count; index++)
            {
                if (_columns[index].AggregateFunction.HasValue)
                    return true;
            }
            return false;
        }
    }

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
    public virtual ISelectClause Clone(SqlClauseContext context)
    {
        var result = CreateClone(context, _columns.Clone(), _distinct);
        result._projectionCountKnown = _projectionCountKnown;
        return result;
    }

    /// <summary>
    /// 创建克隆后的 Select 子句。
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <param name="columns">已深复制的列集合。</param>
    /// <param name="distinct">是否保留去重状态。</param>
    /// <returns>保留 Provider 子类类型的 Select 子句。</returns>
    protected virtual SelectClause CreateClone(SqlClauseContext context, ColumnCollection columns, bool distinct) =>
        new SelectClause(context, columns, distinct);

    /// <summary>
    /// 过滤重复记录
    /// </summary>
    public void Distinct()
    {
        _context.UseOperation(SqlOperationAction.Select);
        _distinct = true;
    }

    /// <inheritdoc />
    public void CountAll(string alias = null) => Aggregate(SqlAggregateFunction.Count, "*", alias);

    /// <inheritdoc />
    public void CountColumn(string column, string alias = null, bool distinct = false)
    {
        if (string.Equals(column?.Trim(), "*", StringComparison.Ordinal))
            throw new ArgumentException("CountColumn 不支持通配符参数，请使用 CountAll。", nameof(column));
        Aggregate(SqlAggregateFunction.Count, column, alias, distinct);
    }

    /// <inheritdoc />
    public void Count<TEntity>(Expression<Func<TEntity, object>> expression, string alias = null,
        bool distinct = false) where TEntity : class =>
        Aggregate(SqlAggregateFunction.Count, expression, alias, distinct);

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
        _context.ValidateOperation(SqlOperationAction.Select);
        SqlAggregateArgumentValidator.ValidateFunction(function);
        if (SqlAggregateArgumentValidator.ValidateWildcard(function, column, distinct, nameof(column)))
        {
            _columns.AddAggregationColumn(function, null, columnAlias, distinct, wildcard: true);
            _context.UseOperation(SqlOperationAction.Select);
            return;
        }
        _columns.AddStructuredAggregationColumn(function, SqlAggregateArgumentValidator.ParseStructuredColumn(column),
            columnAlias, distinct, useDefaultAlias: false);
        _context.UseOperation(SqlOperationAction.Select);
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
        _context.ValidateOperation(SqlOperationAction.Select);
        SqlAggregateArgumentValidator.ValidateFunction(function);
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        _columns.AddStructuredAggregationColumn(function,
            SqlAggregateArgumentValidator.ParseStructuredColumn(_resolver.GetColumn(expression)), columnAlias,
            distinct, typeof(TEntity), useDefaultAlias: false);
        _context.UseOperation(SqlOperationAction.Select);
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
        _context.ValidateOperation(SqlOperationAction.Select);
        SqlAggregateArgumentValidator.ValidateFunction(function);
        argumentSql = SqlAggregateArgumentValidator.ValidateExpression(argumentSql, nameof(argumentSql));
        SqlAggregateArgumentValidator.ValidateWildcard(function, argumentSql, distinct, nameof(argumentSql));
        _columns.AddAggregationColumn(function, argumentSql, columnAlias, distinct, argumentRaw: true);
        _context.UseOperation(SqlOperationAction.Select);
    }

    /// <inheritdoc />
    public void AggregateExpression(SqlAggregateFunction function, string expressionSql, string columnAlias = null,
        bool distinct = false)
    {
        _context.ValidateOperation(SqlOperationAction.Select);
        SqlAggregateArgumentValidator.ValidateFunction(function);
        expressionSql = SqlAggregateArgumentValidator.ValidateExpression(expressionSql, nameof(expressionSql));
        SqlAggregateArgumentValidator.ValidateWildcard(function, expressionSql, distinct, nameof(expressionSql));
        _columns.AddAggregationColumn(function, SqlExpressionIdentifierResolver.Resolve(expressionSql, _dialect), columnAlias, distinct,
            argumentRaw: true);
        _context.UseOperation(SqlOperationAction.Select);
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Sum(string column, string columnAlias = null, bool distinct = false) =>
        Aggregate(SqlAggregateFunction.Sum, column, columnAlias, distinct);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Sum<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class =>
        Aggregate(SqlAggregateFunction.Sum, expression, columnAlias, distinct);

    /// <summary>
    /// 求平均值
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Avg(string column, string columnAlias = null, bool distinct = false) =>
        Aggregate(SqlAggregateFunction.Avg, column, columnAlias, distinct);

    /// <summary>
    /// 求平均值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Avg<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class =>
        Aggregate(SqlAggregateFunction.Avg, expression, columnAlias, distinct);

    /// <summary>
    /// 求最大值
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Max(string column, string columnAlias = null, bool distinct = false) =>
        Aggregate(SqlAggregateFunction.Max, column, columnAlias, distinct);

    /// <summary>
    /// 求最大值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Max<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class =>
        Aggregate(SqlAggregateFunction.Max, expression, columnAlias, distinct);

    /// <summary>
    /// 求最小值
    /// </summary>
    /// <param name="column">列</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Min(string column, string columnAlias = null, bool distinct = false) =>
        Aggregate(SqlAggregateFunction.Min, column, columnAlias, distinct);

    /// <summary>
    /// 求最小值
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    public void Min<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class =>
        Aggregate(SqlAggregateFunction.Min, expression, columnAlias, distinct);

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <param name="columns">列名</param>
    /// <param name="tableAlias">表别名</param>
    public void Select(string columns, string tableAlias = null)
    {
        if (string.IsNullOrWhiteSpace(columns))
            return;
        _context.UseOperation(SqlOperationAction.Select);
        if (columns.Contains("*") || columns.Contains("(") || columns.Contains(")"))
            _projectionCountKnown = false;
        _columns.AddColumns(columns, tableAlias);
    }

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    public void Select<TEntity>(bool propertyAsAlias = false)
    {
        var columns = _resolver.GetColumns<TEntity>(propertyAsAlias);
        if (string.IsNullOrWhiteSpace(columns))
            return;
        _context.UseOperation(SqlOperationAction.Select);
        _columns.AddColumns(columns, typeof(TEntity));
    }

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    public void Select<TEntity>(Expression<Func<TEntity, object[]>> expression, bool propertyAsAlias = false) where TEntity : class
    {
        _context.ValidateOperation(SqlOperationAction.Select);
        if (expression == null)
            return;
        var columns = _resolver.GetColumns(expression, propertyAsAlias);
        if (string.IsNullOrWhiteSpace(columns))
            return;
        _columns.AddColumns(columns, tableType: typeof(TEntity));
        _context.UseOperation(SqlOperationAction.Select);
    }

    /// <summary>
    /// 设置列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <param name="columnAlias">列别名</param>
    public void Select<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null) where TEntity : class
    {
        _context.ValidateOperation(SqlOperationAction.Select);
        if (expression == null)
            return;
        var body = expression.Body is UnaryExpression
        {
            NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
        } unary ? unary.Operand : expression.Body;
        if (body is NewExpression creation && creation.Arguments.Count > 0)
        {
            var columns = new List<string>();
            foreach (var argument in creation.Arguments)
            {
                var value = argument is UnaryExpression
                {
                    NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
                } conversion ? conversion.Operand : argument;
                var lambda = Expression.Lambda<Func<TEntity, object>>(Expression.Convert(value, typeof(object)),
                    expression.Parameters);
                var column = _resolver.GetColumn(lambda);
                if (string.IsNullOrWhiteSpace(column) == false)
                    columns.Add(column);
            }
            if (columns.Count == 0)
                return;
            columns.ForEach(column => _columns.AddColumns(column, typeof(TEntity)));
            _context.UseOperation(SqlOperationAction.Select);
            return;
        }
        if (body is MemberInitExpression memberInit)
        {
            AddMemberInitColumns<TEntity>(memberInit, expression.Parameters);
            _context.UseOperation(SqlOperationAction.Select);
            return;
        }
        var resolvedColumn = _resolver.GetColumn(expression);
        if (string.IsNullOrWhiteSpace(resolvedColumn))
            return;
        _columns.AddColumns(resolvedColumn, typeof(TEntity), columnAlias);
        _context.UseOperation(SqlOperationAction.Select);
    }

    /// <summary>
    /// 固定指定实体类型的既有投影表别名。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="tableAlias">表别名。</param>
    internal void FreezeEntityAlias(Type entityType, string tableAlias) => _columns.FreezeTableAlias(entityType, tableAlias);

    /// <summary>
    /// 添加 DTO 成员初始化投影列。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="memberInit">DTO 成员初始化表达式。</param>
    /// <param name="parameters">实体参数表达式集合。</param>
    private void AddMemberInitColumns<TEntity>(MemberInitExpression memberInit,
        IReadOnlyList<ParameterExpression> parameters) where TEntity : class
    {
        var items = new List<ColumnItem>();
        foreach (var binding in memberInit.Bindings)
        {
            if (binding is not MemberAssignment assignment)
                throw CreateUnsupportedDtoProjectionException(binding.BindingType.ToString());

            var source = UnwrapConversion(assignment.Expression);
            if (source is not MemberExpression member || IsDirectParameterMember(member, parameters) == false)
                throw CreateUnsupportedDtoProjectionException(source.NodeType.ToString());

            var lambda = Expression.Lambda<Func<TEntity, object>>(Expression.Convert(member, typeof(object)), parameters);
            var column = _resolver.GetColumn(lambda);
            if (string.IsNullOrWhiteSpace(column))
                throw CreateUnsupportedDtoProjectionException(source.NodeType.ToString());

            var item = new SqlItem(column, alias: assignment.Member.Name);
            items.Add(ColumnItem.CreateColumn(item.Name, columnAlias: item.Alias, tableType: typeof(TEntity)));
        }
        items.ForEach(_columns.AddColumn);
    }

    /// <summary>
    /// 解包转换表达式。
    /// </summary>
    /// <param name="expression">表达式。</param>
    /// <returns>已解包的表达式。</returns>
    private static Expression UnwrapConversion(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
            expression = conversion.Operand;
        return expression;
    }

    /// <summary>
    /// 检查成员访问是否直接来自当前实体参数。
    /// </summary>
    /// <param name="member">成员访问表达式。</param>
    /// <param name="parameters">实体参数表达式集合。</param>
    /// <returns>如果成员直接来自当前实体参数，返回 true。</returns>
    private static bool IsDirectParameterMember(MemberExpression member, IReadOnlyList<ParameterExpression> parameters) =>
        parameters.Count == 1 && ReferenceEquals(UnwrapConversion(member.Expression), parameters[0]);

    /// <summary>
    /// 创建不支持 DTO 投影表达式的异常。
    /// </summary>
    /// <param name="nodeType">不支持的表达式节点类型。</param>
    /// <returns>异常对象。</returns>
    private static NotSupportedException CreateUnsupportedDtoProjectionException(string nodeType) =>
        new($"不支持的 DTO 投影表达式节点类型：{nodeType}。仅支持当前实体的直接成员赋值。");

    /// <summary>
    /// 设置子查询列
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="columnAlias">列别名</param>
    public void Select(ISqlBuilder builder, string columnAlias)
    {
        _context.ValidateOperation(SqlOperationAction.Select);
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
        _context.ValidateOperation(SqlOperationAction.Select);
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
        _context.ValidateOperation(SqlOperationAction.Select);
        sql = Helper.ResolveSql(sql, _dialect);
        _context.UseOperation(SqlOperationAction.Select);
        _projectionCountKnown = false;
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

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var distinct = GetDistinct();
        var columns = GetColumns();
        builder.Append("Select ").Append(distinct).Append(columns);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _columns.Clear();
        _distinct = false;
        _projectionCountKnown = true;
    }

    /// <summary>
    /// 输出Sql。
    /// </summary>
    public string ToSql()
    {
        var result = new StringBuilder();
        AppendTo(result);
        return result.ToString();
    }

    /// <summary>
    /// 获取Distinct
    /// </summary>
    private string GetDistinct() => _distinct ? "Distinct " : null;

    /// <summary>
    /// 获取列名
    /// </summary>
    protected virtual string GetColumns() => _columns.Count == 0 ? "*" : _columns.ToSql(_dialect, _register);
}