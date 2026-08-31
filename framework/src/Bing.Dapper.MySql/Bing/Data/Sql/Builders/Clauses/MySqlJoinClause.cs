using Bing.Data;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// MySql 表连接子句
/// </summary>
public class MySqlJoinClause : JoinClause
{
    /// <summary>
    /// 是否拆分字符串表名中的句点。
    /// </summary>
    private readonly bool _splitStringTableName;

    /// <summary>
    /// 初始化一个<see cref="MySqlJoinClause"/>类型的实例
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    public MySqlJoinClause(SqlClauseContext context)
        : this(context, (context?.ExecutionContext.DatabaseType ?? Bing.Data.Enums.DatabaseType.MySql) !=
            Bing.Data.Enums.DatabaseType.MySql, null)
    {
    }

    /// <summary>
    /// 使用运行上下文初始化 MySQL 表连接子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="splitStringTableName">是否拆分字符串表名中的句点。</param>
    /// <param name="joinItems">已克隆的连接项。</param>
    protected MySqlJoinClause(SqlClauseContext context, bool splitStringTableName,
        List<JoinItem> joinItems = null)
        : base(context, joinItems)
    {
        _splitStringTableName = splitStringTableName;
    }

    /// <inheritdoc />
    /// <returns>根据 Provider 类型创建的连接项。</returns>
    protected override JoinItem CreateJoinItem(string joinType, string table, string schema, string alias, Type type = null) =>
        _splitStringTableName
            ? JoinItem.CreateTable(joinType, table, schema, alias, type)
            : JoinItem.CreateAtomicTable(joinType, table, schema, alias, type);

    /// <summary>
    /// 解析实际 MySQL 的反引号字符串表名。
    /// </summary>
    /// <param name="table">表名。</param>
    /// <param name="alias">别名。</param>
    /// <returns>表名、别名和架构名。</returns>
    protected override (string TableName, string Alias, string Schema) ParseTableName(string table, string alias)
    {
        if (_splitStringTableName)
            return base.ParseTableName(table, alias);
        var parsedTable = MySqlTableNameParser.Parse(table, alias);
        return (parsedTable.TableName, parsedTable.Alias, parsedTable.Schema);
    }

    /// <inheritdoc />
    /// <returns>当前连接子句的 MySQL 副本。</returns>
    protected override JoinClause CreateClone(SqlClauseContext context, List<JoinItem> joinItems) =>
        new MySqlJoinClause(context, _splitStringTableName, joinItems);
}
