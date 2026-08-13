using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations.Clauses;

/// <summary>
/// 默认 Update 目标表子句。
/// </summary>
public sealed class UpdateClause : MutationTableClauseBase, IUpdateClause
{
    /// <summary>
    /// 初始化一个 <see cref="UpdateClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public UpdateClause(SqlMutationContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public SqlTableReference Table { get; private set; }

    /// <inheritdoc />
    public void UpdateTable(SqlTableReference table)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));
        Context.UseOperation(SqlOperationAction.Update);
        Table = table;
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        AppendPrefixedTable(builder, "Update ", Table, true);
    }

    /// <inheritdoc />
    public void Clear() => Table = null;

    /// <inheritdoc />
    public IUpdateClause Clone(SqlMutationContext context)
    {
        var result = new UpdateClause(context);
        if (Table != null)
            result.UpdateTable(Table with { });
        return result;
    }

    /// <inheritdoc />
    public void Validate(SqlValidationContext context) => ValidateTable(Table, "Update");
}