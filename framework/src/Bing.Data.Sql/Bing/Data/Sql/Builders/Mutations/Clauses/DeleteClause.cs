using System.Text;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations.Clauses;

/// <summary>
/// 默认 Delete 目标表子句。
/// </summary>
public sealed class DeleteClause : MutationTableClauseBase, IDeleteClause
{
    /// <summary>
    /// 初始化一个 <see cref="DeleteClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public DeleteClause(SqlMutationContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public SqlTableReference Table { get; private set; }

    /// <inheritdoc />
    public void From(SqlTableReference table) => Table = table ?? throw new ArgumentNullException(nameof(table));

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        builder.Append("Delete From ");
        AppendTable(builder, Table);
    }

    /// <inheritdoc />
    public void Clear() => Table = null;

    /// <inheritdoc />
    public IDeleteClause Clone(SqlMutationContext context)
    {
        var result = new DeleteClause(context);
        if (Table != null)
            result.From(Table with { });
        return result;
    }

    /// <inheritdoc />
    public void Validate(SqlValidationContext context) => ValidateTable(Table, "Delete");
}