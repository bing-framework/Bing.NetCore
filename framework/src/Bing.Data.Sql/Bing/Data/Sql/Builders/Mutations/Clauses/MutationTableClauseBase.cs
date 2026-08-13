using System.Text;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations.Clauses;

/// <summary>
/// Mutation 目标表子句的共享实现。
/// </summary>
public abstract class MutationTableClauseBase
{
    /// <summary>
    /// 初始化一个 <see cref="MutationTableClauseBase"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    protected MutationTableClauseBase(SqlMutationContext context) => Context = context ?? throw new ArgumentNullException(nameof(context));

    /// <summary>
    /// Mutation 子句上下文。
    /// </summary>
    protected SqlMutationContext Context { get; }

    /// <summary>
    /// 按当前 Provider 格式化表引用。
    /// </summary>
    /// <param name="builder">SQL 输出缓冲区。</param>
    /// <param name="table">结构化表引用。</param>
    protected void AppendTable(StringBuilder builder, SqlTableReference table)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (table == null)
            throw new InvalidOperationException("未指定写操作目标表。");
        Context.Services.TableReferenceValidator.Validate(table, Context.Provider.DatabaseType);
        builder.Append(Context.Services.ObjectNameFormatter.Format(table, Context.Dialect, Context.Provider.DatabaseType));
    }

    /// <summary>
    /// 按当前 Provider 格式化表引用并显式追加别名。
    /// </summary>
    /// <param name="builder">SQL 输出缓冲区。</param>
    /// <param name="table">结构化表引用。</param>
    protected void AppendAliasedTable(StringBuilder builder, SqlTableReference table)
    {
        if (table == null)
            throw new InvalidOperationException("未指定写操作目标表。");
        AppendTable(builder, table with { Alias = null });
        if (string.IsNullOrWhiteSpace(table.Alias) == false)
            builder.Append(" As ").Append(Context.Dialect.SafeName(table.Alias));
    }

    /// <summary>
    /// 以指定关键字原子追加结构化表引用。
    /// </summary>
    /// <param name="builder">SQL 输出缓冲区。</param>
    /// <param name="prefix">写操作关键字前缀。</param>
    /// <param name="table">结构化表引用。</param>
    /// <param name="withAlias">是否显式输出表别名。</param>
    internal void AppendPrefixedTable(StringBuilder builder, string prefix, SqlTableReference table, bool withAlias)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var startIndex = builder.Length;
        try
        {
            builder.Append(prefix);
            if (withAlias)
                AppendAliasedTable(builder, table);
            else
                AppendTable(builder, table);
        }
        catch
        {
            builder.Remove(startIndex, builder.Length - startIndex);
            throw;
        }
    }

    /// <summary>
    /// 验证写操作目标表。
    /// </summary>
    /// <param name="table">结构化表引用。</param>
    /// <param name="operation">写操作名称。</param>
    protected static void ValidateTable(SqlTableReference table, string operation)
    {
        if (table == null || string.IsNullOrWhiteSpace(table.TableName))
            throw new InvalidOperationException($"{operation} 未指定目标表。");
    }
}