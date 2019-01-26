using Bing.Datas.Sql.Queries;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Datas.Sql.Queries.Builders.Clauses;
using Bing.Datas.Sql.Queries.Builders.Core;

namespace Bing.Datas.Dapper.MySql
{
    /// <summary>
    /// MySql From子句
    /// </summary>
    public class MySqlFromClause:FromClause
    {
        /// <summary>
        /// 初始化一个<see cref="MySqlFromClause"/>类型的实例
        /// </summary>
        /// <param name="builder">Sql生成器</param>
        /// <param name="dialect">方言</param>
        /// <param name="resolver">实体解析器</param>
        /// <param name="register">实体别名注册器</param>
        /// <param name="table">表</param>
        public MySqlFromClause(ISqlBuilder builder, IDialect dialect, IEntityResolver resolver,
            IEntityAliasRegister register, SqlItem table = null) : base(
            builder, dialect, resolver, register, table)
        {
        }

        /// <summary>
        /// 创建Sql项
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="schema">架构名</param>
        /// <param name="alias">别名</param>
        /// <returns></returns>
        protected override SqlItem CreateSqlItem(string table, string schema, string alias)
        {
            return new SqlItem(table, schema, alias, false, false);
        }

        /// <summary>
        /// 克隆
        /// </summary>
        /// <param name="builder">Sql生成器</param>
        /// <param name="register">实体别名注册器</param>
        /// <returns></returns>
        public override IFromClause Clone(ISqlBuilder builder, IEntityAliasRegister register)
        {
            return new MySqlFromClause(builder, Dialect, Resolver, register, Table);
        }
    }
}
