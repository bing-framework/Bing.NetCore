using Bing.Data;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// Sqlite 表连接子句
/// </summary>
public class SqliteJoinClause : JoinClause
{
    /// <summary>
    /// 初始化一个 <see cref="SqliteJoinClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">SQL 子句运行上下文。</param>
    public SqliteJoinClause(SqlClauseContext context)
        : this(context, null)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="SqliteJoinClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="joinItems">已克隆的连接项。</param>
    protected SqliteJoinClause(SqlClauseContext context, List<JoinItem> joinItems)
        : base(context, joinItems)
    {
    }

    /// <inheritdoc />
    protected override JoinItem CreateJoinItem(string joinType, string table, string schema, string alias, Type type = null) =>
        JoinItem.CreateTable(joinType, table, schema, alias, type);

    /// <inheritdoc />
    protected override JoinClause CreateClone(SqlClauseContext context, List<JoinItem> joinItems) =>
        new SqliteJoinClause(context, joinItems);
}
