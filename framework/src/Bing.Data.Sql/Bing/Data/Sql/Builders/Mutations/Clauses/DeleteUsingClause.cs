using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations.Clauses;

/// <summary>
/// 默认 Delete Using 来源子句。
/// </summary>
public sealed class DeleteUsingClause : MutationTableClauseBase, IDeleteUsingClause
{
    /// <summary>
    /// 初始化一个 <see cref="DeleteUsingClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public DeleteUsingClause(SqlMutationContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public SqlTableReference Table { get; private set; }

    /// <inheritdoc />
    public void Using(SqlTableReference table)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));
        Context.UseOperation(SqlOperationAction.DeleteUsing);
        Table = table;
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        builder.Append(" Using ");
        AppendAliasedTable(builder, Table);
    }

    /// <inheritdoc />
    public void Clear() => Table = null;

    /// <inheritdoc />
    public IDeleteUsingClause Clone(SqlMutationContext context)
    {
        var result = new DeleteUsingClause(context);
        if (Table != null)
            result.Using(Table with { });
        return result;
    }

    /// <inheritdoc />
    public void Validate(SqlValidationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        ValidateTable(Table, "Delete Using");
        if (string.IsNullOrWhiteSpace(Table.Alias))
            throw new InvalidOperationException("Delete Using 来源表必须指定别名。");
        if (context.Profile.Mutation.SupportsDeleteUsing == false)
            throw new NotSupportedException($"Provider {context.Provider.Key} 不支持 Delete Using。");
    }
}