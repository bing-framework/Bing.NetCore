using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// MySql From子句
/// </summary>
public class MySqlFromClause : FromClause
{
    /// <summary>
    /// 初始化一个<see cref="MySqlFromClause"/>类型的实例
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="dialect">方言</param>
    /// <param name="resolver">实体解析器</param>
    /// <param name="register">实体别名注册器</param>
    /// <param name="table">表</param>
    /// <param name="objectNameFormatter">SQL 对象名称格式化器</param>
    /// <param name="providerDatabaseType">固定数据库类型</param>
    /// <param name="tableReferenceValidator">SQL 表引用验证器</param>
    public MySqlFromClause(
        ISqlBuilder builder, 
        IDialect dialect, 
        IEntityResolver resolver,
        IEntityAliasRegister register,
        SqlItem table = null,
        ISqlObjectNameFormatter objectNameFormatter = null,
        Bing.Data.Enums.DatabaseType? providerDatabaseType = null,
        ISqlTableReferenceValidator tableReferenceValidator = null)
        : base(builder, dialect, resolver, register, table, objectNameFormatter, providerDatabaseType,
            tableReferenceValidator)
    {
    }

    /// <summary>
    /// 创建Sql项
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="schema">架构名</param>
    /// <param name="alias">别名</param>
    protected override SqlItem CreateSqlItem(string table, string schema, string alias) => new SqlItem(table, schema, alias, false, false);

    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="register">实体别名注册器</param>
    public override IFromClause Clone(ISqlBuilder builder, IEntityAliasRegister register)
    {
        if (register != null)
            register.FromType = Register.FromType;
        return new MySqlFromClause(builder, Dialect, Resolver, register, Table?.Clone(), ObjectNameFormatter,
            ProviderDatabaseType, TableReferenceValidator);
    }
}
