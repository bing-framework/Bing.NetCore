using System;
using System.Collections.Generic;
using Bing.Data;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// Oracle 表连接子句
/// </summary>
public class OracleJoinClause : JoinClause
{
    /// <inheritdoc />
    public OracleJoinClause(SqlClauseContext context)
        : this(context, null)
    {
    }

    /// <summary>
    /// 使用运行上下文初始化 Oracle 表连接子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="joinItems">已克隆的连接项。</param>
    protected OracleJoinClause(SqlClauseContext context, List<JoinItem> joinItems)
        : base(context, joinItems)
    {
    }

    /// <inheritdoc />
    protected override JoinItem CreateJoinItem(string joinType, string table, string schema, string alias, Type type = null) =>
        JoinItem.CreateAtomicTable(joinType, table, schema, alias, type);

    /// <inheritdoc />
    protected override JoinClause CreateClone(SqlClauseContext context, List<JoinItem> joinItems) =>
        new OracleJoinClause(context, joinItems);

    /// <inheritdoc />
    protected override string GetSubqueryAlias(string alias) => $" {_dialect.SafeName(alias)}";
}
