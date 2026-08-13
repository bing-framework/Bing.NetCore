using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations.Clauses;

/// <summary>
/// 默认 Update From 来源子句。
/// </summary>
public sealed class UpdateFromClause : MutationTableClauseBase, IUpdateFromClause
{
    /// <summary>
    /// 初始化一个 <see cref="UpdateFromClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public UpdateFromClause(SqlMutationContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public SqlTableReference Table { get; private set; }

    /// <inheritdoc />
    public void From(SqlTableReference table)
    {
        if (table == null)
            throw new ArgumentNullException(nameof(table));
        Context.UseOperation(SqlOperationAction.UpdateFrom);
        Table = table;
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        AppendPrefixedTable(builder, " From ", Table, true);
    }

    /// <inheritdoc />
    public void Clear() => Table = null;

    /// <inheritdoc />
    public IUpdateFromClause Clone(SqlMutationContext context)
    {
        var result = new UpdateFromClause(context);
        if (Table != null)
            result.From(Table with { });
        return result;
    }

    /// <inheritdoc />
    public void Validate(SqlValidationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        ValidateTable(Table, "Update From");
        if (string.IsNullOrWhiteSpace(Table.Alias))
            throw new InvalidOperationException("Update From 来源表必须指定别名。");
        if (context.Profile.Mutation.SupportsUpdateFrom == false)
            throw new NotSupportedException($"Provider {context.Provider.Key} 不支持 Update From。");
    }
}
