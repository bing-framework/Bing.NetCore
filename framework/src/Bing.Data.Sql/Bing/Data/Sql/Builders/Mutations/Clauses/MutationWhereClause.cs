using System.Text;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Mutations.Contexts;

namespace Bing.Data.Sql.Builders.Mutations.Clauses;

/// <summary>
/// 默认 Mutation Where 子句。
/// </summary>
public sealed class MutationWhereClause : IMutationWhereClause
{
    /// <summary>
    /// 当前组合条件。
    /// </summary>
    private ICondition _condition;

    /// <summary>
    /// 初始化一个 <see cref="MutationWhereClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public MutationWhereClause(SqlMutationContext context) => _ = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public bool IsEmpty => string.IsNullOrWhiteSpace(_condition?.GetCondition());

    /// <inheritdoc />
    public void And(ICondition condition)
    {
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        _condition = new AndCondition(_condition, condition);
    }

    /// <inheritdoc />
    public void Or(ICondition condition)
    {
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));
        _condition = new OrCondition(_condition, condition);
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var condition = _condition?.GetCondition();
        if (string.IsNullOrWhiteSpace(condition) == false)
            builder.Append(" Where ").Append(condition);
    }

    /// <inheritdoc />
    public void Clear() => _condition = null;

    /// <inheritdoc />
    public IMutationWhereClause Clone(SqlMutationContext context) => new MutationWhereClause(context)
    {
        _condition = _condition == null ? null : new SqlCondition(_condition.GetCondition())
    };

    /// <inheritdoc />
    public void Validate(SqlValidationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (IsEmpty && context.AllowAllRows == false)
            throw new InvalidOperationException($"拒绝执行无条件 {context.ExecutionKind} 操作。");
    }
}