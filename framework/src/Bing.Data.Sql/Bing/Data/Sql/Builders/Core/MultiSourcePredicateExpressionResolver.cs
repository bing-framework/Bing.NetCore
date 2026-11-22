using System.Linq.Expressions;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Internal;
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
    /// 保存解析多源谓词时使用的 SQL 子句上下文。
    /// </summary>
    private readonly SqlClauseContext _context;
    /// <summary>按当前映射与参数工厂创建比较值。</summary>
    private readonly Helper _helper;

    /// <summary>
    /// 初始化一个 <see cref="MultiSourcePredicateExpressionResolver" /> 类型的实例。
    /// </summary>
    ///
    /// <param name="expression">待解析的谓词表达式。</param>
    ///
    /// <param name="sources">当前查询的根表源。</param>
    ///
    /// <param name="getColumn">解析绑定列的回调。</param>
    ///
    /// <param name="parameterManager">当前查询的参数管理器。</param>
    /// <param name="context">当前操作关联的上下文或工作单元。</param>
    internal MultiSourcePredicateExpressionResolver(LambdaExpression expression, IReadOnlyList<TableSource> sources,
        Func<Expression, TableSource, string> getColumn, IParameterManager parameterManager, SqlClauseContext context)
    {
        _bindings = new SqlParameterBindingScope(expression, sources);
        _context = context;
        _getColumn = getColumn ?? throw new ArgumentNullException(nameof(getColumn));
        _parameterManager = parameterManager ?? throw new ArgumentNullException(nameof(parameterManager));
        _helper = new Helper(context, _parameterManager);
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
        if (SqlFunctionProjectionParser.ContainsAggregate(expression))
            throw new NotSupportedException("Where/Join 条件不能包含聚合，请使用 Having。");
        return ResolveExpression(expression.Body);
    }

    /// <summary>
    /// 递归解析条件表达式。
    /// </summary>
    /// <param name="expression">待解析的条件表达式。</param>
    /// <returns>表达式对应的参数化 SQL 条件。</returns>
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
            if (notExpression.Method != null)
                throw new NotSupportedException("条件取反仅支持内置布尔运算。");
            if (notExpression.Operand is MemberExpression)
            {
                var column = ResolveColumn(notExpression.Operand);
                return new SqlBoundCondition(_context, column, AddParameter(false, column, Operator.Equal), Operator.Equal);
            }
            return new SqlNegatedCondition(ResolveExpression(notExpression.Operand));
        }

        if (expression is MethodCallExpression call && call.Method.DeclaringType == typeof(string) &&
            call.Object != null && call.Arguments.Count == 1 && call.Arguments[0].Type == typeof(string))
        {
            var comparison = call.Method.Name switch
            {
                nameof(string.Contains) => Operator.Contains,
                nameof(string.StartsWith) => Operator.Starts,
                nameof(string.EndsWith) => Operator.Ends,
                _ => throw new NotSupportedException("字符串条件仅支持 Contains、StartsWith 和 EndsWith。")
            };
            var column = ResolveColumn(call.Object);
            var value = Lambdas.GetValue(call.Arguments[0]);
            return new SqlBoundCondition(_context, column, AddParameter(value, column, comparison), comparison);
        }

        if (expression is MethodCallExpression contains && TryGetCollectionContains(contains, out var collection, out var item))
        {
            // 移除引用转换；数值转换与自定义转换不能直接映射为原始 SQL 列。
            while (item is UnaryExpression { NodeType: ExpressionType.Convert, Method: null } conversion &&
                   !conversion.Operand.Type.IsValueType && conversion.Type.IsAssignableFrom(conversion.Operand.Type))
                item = conversion.Operand;
            if (item is not MemberExpression)
                throw new NotSupportedException("集合条件仅支持直接属性列。");
            var column = ResolveColumn(item);
            var values = Lambdas.GetValue(collection);
            return _helper.CreateCondition(column.Column.MemberName, column, column.Source.EntityType, values, Operator.In);
        }

        var booleanColumn = ResolveColumn(expression);
        return new SqlBoundCondition(_context, booleanColumn, AddParameter(true, booleanColumn, Operator.Equal), Operator.Equal);
    }

    /// <summary>识别标准集合包含判断，其他重载保持原有不支持行为。</summary>
    private static bool TryGetCollectionContains(MethodCallExpression call, out Expression collection, out Expression item)
    {
        collection = null;
        item = null;
        if (call.Method.Name != nameof(Enumerable.Contains))
            return false;
        if (call.Method.DeclaringType == typeof(Enumerable) && call.Arguments.Count == 2)
        {
            collection = call.Arguments[0];
            item = call.Arguments[1];
            return true;
        }
        // C# 编译器可能把数组 Contains 绑定为 ReadOnlySpan<T> 的扩展方法。
        if (call.Method.DeclaringType == typeof(MemoryExtensions) && call.Arguments.Count == 2 &&
            call.Arguments[0] is MethodCallExpression conversion && conversion.Method.Name == "op_Implicit" &&
            conversion.Method.DeclaringType?.IsGenericType == true &&
            conversion.Method.DeclaringType.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>) &&
            conversion.Arguments.Count == 1 && conversion.Arguments[0].Type.IsArray)
        {
            collection = conversion.Arguments[0];
            item = call.Arguments[1];
            return true;
        }
        var type = call.Method.DeclaringType;
        if (type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) &&
            call.Object != null && call.Arguments.Count == 1)
        {
            collection = call.Object;
            item = call.Arguments[0];
            return true;
        }
        return false;
    }

    /// <summary>
    /// 解析比较表达式。
    /// </summary>
    /// <param name="expression">待解析的二元表达式。</param>
    /// <returns>比较表达式对应的参数化 SQL 条件。</returns>
    private ICondition ResolveComparison(BinaryExpression expression)
    {
        var @operator = GetOperator(expression.NodeType);
        var leftColumn = _bindings.TryGetSource(expression.Left, out var leftSource)
            ? ResolveColumn(expression.Left, leftSource) : null;
        var rightColumn = _bindings.TryGetSource(expression.Right, out var rightSource)
            ? ResolveColumn(expression.Right, rightSource) : null;
        var left = leftColumn ?? AddParameter(Lambdas.GetValue(expression.Left), rightColumn, @operator);
        var right = rightColumn ?? AddParameter(Lambdas.GetValue(expression.Right), leftColumn, @operator);
        if (leftColumn == null && rightColumn != null && left.Value == null &&
            @operator is Operator.Equal or Operator.NotEqual)
            (left, right) = (right, left);
        return new SqlBoundCondition(_context, left, right, @operator);
    }

    /// <summary>
    /// 解析列表达式。
    /// </summary>
    /// <param name="expression">待解析的列表达式。</param>
    /// <returns>列 SQL。</returns>
    private SqlBoundOperand ResolveColumn(Expression expression)
    {
        if (_bindings.TryGetSource(expression, out var source) == false)
            throw new InvalidOperationException("多表谓词中的列必须引用当前查询的 Lambda 参数。");
        return ResolveColumn(expression, source);
    }

    /// <summary>
    /// 解析列表达式。
    /// </summary>
    /// <param name="expression">待解析的列表达式。</param>
    /// <param name="source">列所属的表来源。</param>
    /// <returns>按表来源解析出的列 SQL。</returns>
    private SqlBoundOperand ResolveColumn(Expression expression, TableSource source) {
        _getColumn(expression, source); // 保留既有映射与表达式验证。
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert } convert) expression = convert.Operand;
        if (expression is not MemberExpression member) throw new NotSupportedException("条件列必须是直接属性。");
        return new SqlBoundOperand(new SqlProjectionColumn(source.SourceId, member.Member.Name, null, member.Type), source, null);
    }

    /// <summary>
    /// 创建参数并返回 SQL 占位符。
    /// </summary>
    /// <param name="value">参数值。</param>
    /// <param name="column">与值比较的逻辑列；缺少实体来源时使用普通参数。</param>
    /// <param name="operator">参数关联的比较运算符。</param>
    /// <returns>参数操作数；空值相等或不等比较使用不带参数的空值操作数。</returns>
    private SqlBoundOperand AddParameter(object value, SqlBoundOperand column, Operator @operator)
    {
        if (value == null)
        {
            if (@operator is Operator.Greater or Operator.GreaterEqual or Operator.Less or Operator.LessEqual)
                throw new ArgumentNullException(nameof(value), "关系比较条件值不能为空。");
            if (@operator is Operator.Equal or Operator.NotEqual)
                return new SqlBoundOperand(null, null, null);
        }
        var name = _parameterManager.GenerateName();
        _helper.AddParameter(_parameterManager, name, value, @operator, column?.Source?.EntityType,
            column?.Column?.MemberName, SqlParameterSource.Lambda);
        return new SqlBoundOperand(null, null, name);
    }

    /// <summary>
    /// 将表达式节点转换为 SQL 比较运算符。
    /// </summary>
    /// <param name="nodeType">表达式节点类型。</param>
    /// <returns>对应的 SQL 比较运算符。</returns>
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
