using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Matedatas;

namespace Bing.Datas.Dapper.Sqlite;

/// <summary>
/// Sqlite From子句
/// </summary>
public class SqliteFromClause : FromClause
{
    /// <summary>
    /// 初始化一个<see cref="FromClause"/>类型的实例
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="dialect">Sql方言</param>
    /// <param name="resolver">实体解析器</param>
    /// <param name="register">实体别名注册器</param>
    /// <param name="tableDatabase">表数据库</param>
    /// <param name="table">表</param>
    public SqliteFromClause(ISqlBuilder builder
        , IDialect dialect
        , IEntityResolver resolver
        , IEntityAliasRegister register
        , ITableDatabase tableDatabase
        , SqlItem table = null)
        : base(builder, dialect, resolver, register, tableDatabase, table)
    {
    }

    /// <summary>
    /// 创建Sql项
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="schema">架构名</param>
    /// <param name="alias">别名</param>
    protected override SqlItem CreateSqlItem(string table, string schema, string alias) =>
        new SqlItem(table, schema, alias, false, false);

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="register">实体别名注册器</param>
    public override IFromClause Clone(ISqlBuilder builder, IEntityAliasRegister register)
    {
        if (register != null)
            register.FromType = Register.FromType;
        return new SqliteFromClause(builder, Dialect, Resolver, register, TableDatabase, Table);
    }
}