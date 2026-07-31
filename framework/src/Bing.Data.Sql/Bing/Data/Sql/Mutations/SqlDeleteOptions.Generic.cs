using System.Linq.Expressions;

namespace Bing.Data.Sql.Mutations;

/// <summary>
/// 删除指定实体时使用的强类型并发选项。
/// </summary>
/// <typeparam name="TEntity">实体类型。</typeparam>
public sealed class SqlDeleteOptions<TEntity> : SqlDeleteOptions where TEntity : class
{
    /// <summary>
    /// 按 CLR 属性名保存的并发原始值。
    /// </summary>
    private readonly Dictionary<string, object> _originalValues = new(StringComparer.Ordinal);

    /// <summary>
    /// 设置一个并发令牌的原始值。
    /// </summary>
    /// <typeparam name="TValue">并发属性值类型。</typeparam>
    /// <param name="selector">实体并发属性表达式。</param>
    /// <param name="value">写入条件中使用的原始值。</param>
    /// <returns>当前选项实例。</returns>
    public SqlDeleteOptions<TEntity> Original<TValue>(Expression<Func<TEntity, TValue>> selector, TValue value)
    {
        _originalValues[GetPropertyName(selector)] = value;
        return this;
    }

    /// <inheritdoc />
    internal override bool TryGetOriginalValue(string propertyName, out object value) =>
        _originalValues.TryGetValue(propertyName, out value);

    /// <summary>
    /// 从简单成员访问表达式解析实体属性名。
    /// </summary>
    private static string GetPropertyName<TValue>(Expression<Func<TEntity, TValue>> selector)
    {
        if (selector == null)
            throw new ArgumentNullException(nameof(selector));
        var expression = selector.Body is UnaryExpression { NodeType: ExpressionType.Convert } unary
            ? unary.Operand
            : selector.Body;
        if (expression is not MemberExpression { Expression: ParameterExpression } member)
            throw new ArgumentException("并发原始值必须指定实体的直接属性访问。", nameof(selector));
        return member.Member.Name;
    }
}