namespace Bing.Datas.Sql.Queries.Builders.Core
{
    // From(设置表名)
    public partial class SqlBuilderBase
    {
        /// <summary>
        /// 设置表名
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="alias">别名</param>
        /// <returns></returns>
        public virtual ISqlBuilder From(string table, string alias = null)
        {
            FromClause.From(table, alias);
            return this;
        }

        /// <summary>
        /// 设置表名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="alias">别名</param>
        /// <param name="schema">架构名</param>
        /// <returns></returns>
        public virtual ISqlBuilder From<TEntity>(string alias = null, string schema = null) where TEntity : class
        {
            FromClause.From<TEntity>(alias, schema);
            return this;
        }

        /// <summary>
        /// 添加到From子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        public virtual ISqlBuilder AppendFrom(string sql)
        {
            FromClause.AppendSql(sql);
            return this;
        }
    }
}
