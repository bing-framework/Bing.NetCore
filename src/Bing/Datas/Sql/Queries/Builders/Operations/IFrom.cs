using System;

namespace Bing.Datas.Sql.Queries.Builders.Operations
{
    /// <summary>
    /// 设置表名
    /// </summary>
    public interface IFrom
    {
        /// <summary>
        /// 设置表名
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="alias">别名</param>
        /// <returns></returns>
        ISqlBuilder From(string table, string alias = null);

        /// <summary>
        /// 设置表名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="alias">别名</param>
        /// <param name="schema">架构名</param>
        /// <returns></returns>
        ISqlBuilder From<TEntity>(string alias = null, string schema = null) where TEntity : class;

        /// <summary>
        /// 添加到From子句
        /// </summary>
        /// <param name="builder">Sql生成器</param>
        /// <param name="alias">表别名</param>
        /// <returns></returns>
        ISqlBuilder From(ISqlBuilder builder, string alias);

        /// <summary>
        /// 添加到From子句
        /// </summary>
        /// <param name="action">子查询操作</param>
        /// <param name="alias">表别名</param>
        /// <returns></returns>
        ISqlBuilder From(Action<ISqlBuilder> action, string alias);

        /// <summary>
        /// 添加到From子句
        /// </summary>
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlBuilder AppendFrom(string sql);

        
    }
}
