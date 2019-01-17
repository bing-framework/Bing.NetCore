using Bing.Datas.Sql.Queries.Builders.Abstractions;

namespace Bing.Datas.Sql.Queries
{
    /// <summary>
    /// Sql查询对象扩展 - 杂项
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 创建一个新的Sql生成器
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <returns></returns>
        public static ISqlBuilder NewBuilder(this ISqlQuery sqlQuery)
        {
            var builder = sqlQuery.GetBuilder();
            return builder.New();
        }

        /// <summary>
        /// 复制Sql生成器
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <returns></returns>
        public static ISqlBuilder CloneBuilder(this ISqlQuery sqlQuery)
        {
            var builder = sqlQuery.GetBuilder();
            return builder.Clone();
        }

        /// <summary>
        /// 获取调试Sql语句
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <returns></returns>
        public static string GetDebugSql(this ISqlQuery sqlQuery)
        {
            var builder = sqlQuery.GetBuilder();
            return builder.ToDebugSql();
        }

        /// <summary>
        /// 清空并初始化。用于多次执行不同Sql语句，当执行完一个Sql语句后，清空即可继续使用
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <returns></returns>
        public static ISqlQuery Clear(this ISqlQuery sqlQuery)
        {
            var builder = sqlQuery.GetBuilder();
            builder.Clear();
            return sqlQuery;
        }
    }
}
