using System.Linq.Expressions;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Params;
using Bing.Expressions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 将多表 Lambda 谓词解析为参数化 SQL 条件。
/// </summary>
/// <remarks>
/// 仅绑定 Lambda 参数成员为列引用，其他值始终进入参数管理器，避免表达式值直接拼接到 SQL 中。
/// </remarks>
internal sealed class MultiSourcePredicateExpressionResolver
{
    /// <summary>
    /// 参数到表源的绑定范围。
    /// </summary>
    private readonly SqlParameterBindingScope _bindings;

    /// <summary>
    /// 解析绑定表源列的回调。
    /// </summary>
    private readonly Func<Expression, TableSource, string> _getColumn;

    /// <summary>
    /// 参数管理器。
    /// </summary>
    private readonly IParameterManager _parameterManager;

    /// <summary>
    /// 初始化多表谓词解析器。
    /// </summary>
    /// <param name="expression">待解析的谓词表达式。</param>
    /// <param name="sources">当前查询的根表源。</param>
    /// <param name="getColumn">解析绑定列的回调。</param>
    /// <param name="parameterManager">当前查询的参数管理器。</param>
    internal MultiSourcePredicateExpressionResolver(LambdaExpression expression, IReadOnlyList<TableSource> sources,
        Func<Expression, TableSource, string> getColumn, IParameterManager parameterManager)
    {
        _bindings = new SqlParameterBindingScope(expression, sources);
        _getColumn = getColumn ?? throw new ArgumentNullException(nameof(getColumn));
        _parameterManager = parameterManager ?? throw new ArgumentNullException(nameof(parameterManager));
    }

    /// <summary>
    /// 解析谓词表达式。
    /// </summary>
    /// <param name="expression">谓词表达式。</param>
    /// <returns>参数化 SQL 条件。</returns>
    internal ICondition Resolve(LambdaExpression expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        return ResolveExpression(expression.Body);
    }

    /// <summary>
    /// 递归解析条件表达式。
    /// </summary>
    private ICondition ResolveExpression(Expression expression)
    {
        if (expression is BinaryExpression binary)
        {
            if (binary.NodeType == ExpressionType.AndAlso)
                return new AndCondition(ResolveExpression(binary.Left), ResolveExpression(binary.Right));
            if (binary.NodeType == ExpressionType.OrElse)
                return new OrCondition(ResolveExpression(binary.Left), ResolveExpression(binary.Right));
            return ResolveComparison(binary);
        }

        if (expression is UnaryExpression { NodeType: ExpressionType.Not } notExpression)
        {
            var column = ResolveColumn(notExpression.Operand);
            return SqlConditionFactory.Create(column, AddParameter(false), Operator.Equal);
        }

        var booleanColumn = ResolveColumn(expression);
        return SqlConditionFactory.Create(booleanColumn, AddParameter(true), Operator.Equal);
    }

    /// <summary>
    /// 解析比较表达式。
    /// </summary>
    private ICondition ResolveComparison(BinaryExpression expression)
    {
        var @operator = GetOperator(expression.NodeType);
        var left = ResolveOperand(expression.Left);
        var right = ResolveOperand(expression.Right);
        return SqlConditionFactory.Create(left, right, @operator);
    }

    /// <summary>
    /// 解析列或参数操作数。
    /// </summary>
    private string ResolveOperand(Expression expression) =>
        _bindings.TryGetSource(expression, out var source) ? ResolveColumn(expression, source) : AddParameter(Lambdas.GetValue(expression));

    /// <summary>
    /// 解析绑定列。
    /// </summary>
    private string ResolveColumn(Expression expression)
    {
        if (_bindings.TryGetSource(expression, out var source) == false)
            throw new InvalidOperationException("多表谓词中的列必须引用当前查询的 Lambda 参数。");
        return ResolveColumn(expression, source);
    }

    /// <summary>
    /// 使用来源绑定解析列。
    /// </summary>
    private string ResolveColumn(Expression expression, TableSource source) => _getColumn(expression, source);

    /// <summary>
    /// 创建参数并返回 SQL 占位符。
    /// </summary>
    private string AddParameter(object value)
    {
        if (value == null)
            return null;
        var name = _parameterManager.GenerateName();
        _parameterManager.Add(name, value);
        return name;
    }

    /// <summary>
    /// 将表达式节点转换为 SQL 比较运算符。
    /// </summary>
    private static Operator GetOperator(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal => Operator.Equal,
        ExpressionType.NotEqual => Operator.NotEqual,
        ExpressionType.GreaterThan => Operator.Greater,
        ExpressionType.GreaterThanOrEqual => Operator.GreaterEqual,
        ExpressionType.LessThan => Operator.Less,
        ExpressionType.LessThanOrEqual => Operator.LessEqual,
        _ => throw new NotSupportedException($"不支持的多表谓词运算符: {nodeType}。")
    };
}