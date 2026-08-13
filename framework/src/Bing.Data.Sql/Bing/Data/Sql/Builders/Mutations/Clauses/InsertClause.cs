using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations.Clauses;

/// <summary>
/// 默认 Insert 目标表子句。
/// </summary>
public sealed class InsertClause : MutationTableClauseBase, IInsertClause
{
    /// <summary>
    /// 初始化一个 <see cref="InsertClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public InsertClause(SqlMutationContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public SqlTableReference Table { get; private set; }

    /// <inheritdoc />
    public void Into(SqlTableReference table)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));
        Context.UseOperation(SqlOperationAction.InsertInto);
        Table = table;
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        AppendPrefixedTable(builder, "Insert Into ", Table, false);
    }

    /// <inheritdoc />
    public void Clear() => Table = null;

    /// <inheritdoc />
    public IInsertClause Clone(SqlMutationContext context)
    {
        var result = new InsertClause(context);
        if (Table != null)
            result.Into(Table with { });
        return result;
    }

    /// <inheritdoc />
    public void Validate(SqlValidationContext context) => ValidateTable(Table, "Insert");
}