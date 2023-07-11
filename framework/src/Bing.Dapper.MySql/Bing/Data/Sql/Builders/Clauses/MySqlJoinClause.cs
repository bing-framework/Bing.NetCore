using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// MySql 表连接子句
/// </summary>
public class MySqlJoinClause : JoinClause
{
    /// <summary>
    /// 初始化一个<see cref="MySqlJoinClause"/>类型的实例
    /// </summary>
    /// <param name="sqlBuilder">Sql生成器</param>
    /// <param name="dialect">方言</param>
    /// <param name="resolver">实体解析器</param>
    /// <param name="register">实体别名注册器</param>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="tableDatabase">表数据库</param>
    public MySqlJoinClause(ISqlBuilder sqlBuilder, IDialect dialect, IEntityResolver resolver, IEntityAliasRegister register, IParameterManager parameterManager, ITableDatabase tableDatabase)
        : base(sqlBuilder, dialect, resolver, register, parameterManager, tableDatabase)
    {
    }

    /// <inheritdoc />
    protected override JoinItem CreateJoinItem(string joinType, string table, string schema, string alias, Type type = null) =>
        new JoinItem(joinType, table, schema, alias, false, false, type);
}
