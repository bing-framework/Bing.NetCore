using System;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Matedatas;

namespace Bing.Datas.Dapper.Oracle;

/// <summary>
/// Oracle 表连接子句
/// </summary>
public class OracleJoinClause : JoinClause
{
    /// <summary>
    /// 初始化一个<see cref="OracleJoinClause"/>类型的的实例
    /// </summary>
    /// <param name="sqlBuilder">Sql生成器</param>
    /// <param name="dialect">Sql方言</param>
    /// <param name="resolver">实体解析器</param>
    /// <param name="register">实体注册器</param>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="tableDatabase">表数据库</param>
    public OracleJoinClause(ISqlBuilder sqlBuilder, IDialect dialect, IEntityResolver resolver,
        IEntityAliasRegister register, IParameterManager parameterManager, ITableDatabase tableDatabase) : base(
        sqlBuilder, dialect, resolver, register, parameterManager, tableDatabase)
    {
    }

    /// <summary>
    /// 创建连接项
    /// </summary>
    /// <param name="joinType">连接类型</param>
    /// <param name="table">表名</param>
    /// <param name="schema">架构名</param>
    /// <param name="alias">别名</param>
    /// <param name="type">类型</param>
    protected override JoinItem CreateJoinItem(string joinType, string table, string schema, string alias, Type type = null) => new JoinItem(joinType, table, schema, alias, false, false, type);
}