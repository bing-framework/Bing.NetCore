using System.Linq.Expressions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 将 Lambda 参数绑定到查询图中的具体表源实例。
/// </summary>
/// <remarks>
/// 绑定按参数位置而非 CLR 类型建立，因此可准确区分同一实体类型的多个来源。
/// </remarks>
internal sealed class SqlParameterBindingScope
{
    /// <summary>
    /// 参数与表源实例的绑定关系。
    /// </summary>
    private readonly IReadOnlyDictionary<ParameterExpression, TableSource> _bindings;

    /// <summary>
    /// 使用 Lambda 表达式参数与表源实例创建绑定范围。
    /// </summary>
    /// <param name="expression">定义参数位置的 Lambda 表达式。</param>
    /// <param name="sources">按参数顺序排列的表源实例。</param>
    internal SqlParameterBindingScope(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        if (sources == null)
            throw new ArgumentNullException(nameof(sources));
        if (expression.Parameters.Count != sources.Count)
            throw new ArgumentException("Lambda 参数数量必须与表源数量一致。", nameof(sources));

        var bindings = new Dictionary<ParameterExpression, TableSource>();
        for (var index = 0; index < expression.Parameters.Count; index++)
        {
            var parameter = expression.Parameters[index];
            var source = sources[index] ?? throw new ArgumentException("表源不能为空。", nameof(sources));
            if (source.EntityType != parameter.Type)
                throw new ArgumentException("Lambda 参数类型必须与对应表源实体类型一致。", nameof(sources));
            bindings.Add(parameter, source);
        }

        _bindings = bindings;
    }

    /// <summary>
    /// 尝试解析表达式所属的表源实例。
    /// </summary>
    /// <param name="expression">成员、转换或参数表达式。</param>
    /// <param name="source">解析到的表源实例。</param>
    /// <returns>成功找到被绑定参数时返回 true。</returns>
    internal bool TryGetSource(Expression expression, out TableSource source)
    {
        source = null;
        var parameter = FindParameter(expression);
        return parameter != null && _bindings.TryGetValue(parameter, out source);
    }

    /// <summary>
    /// 获取表达式所属的表源实例。
    /// </summary>
    /// <param name="expression">成员、转换或参数表达式。</param>
    /// <returns>已绑定的表源实例。</returns>
    /// <exception cref="InvalidOperationException">表达式未引用当前范围的 Lambda 参数时抛出。</exception>
    internal TableSource GetSource(Expression expression)
    {
        if (TryGetSource(expression, out var source))
            return source;
        throw new InvalidOperationException("表达式未绑定到当前查询的表源实例。");
    }

    /// <summary>
    /// 向内查找表达式引用的 Lambda 参数。
    /// </summary>
    /// <param name="expression">待查找的表达式。</param>
    /// <returns>表达式引用的 Lambda 参数；未找到时返回 <see langword="null"/>。</returns>
    private static ParameterExpression FindParameter(Expression expression)
    {
        while (expression != null)
        {
            switch (expression)
            {
                case ParameterExpression parameter:
                    return parameter;
                case MemberExpression member:
                    expression = member.Expression;
                    break;
                case UnaryExpression unary:
                    expression = unary.Operand;
                    break;
                default:
                    return null;
            }
        }

        return null;
    }
}