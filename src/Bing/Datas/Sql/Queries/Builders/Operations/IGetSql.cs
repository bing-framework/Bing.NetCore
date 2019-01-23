namespace Bing.Datas.Sql.Queries.Builders.Operations
{
    /// <summary>
    /// 获取Sql语句
    /// </summary>
    public interface IGetSql
    {
        /// <summary>
        /// 获取Select语句
        /// </summary>
        /// <returns></returns>
        string GetSelect();

        /// <summary>
        /// 获取From语句
        /// </summary>
        /// <returns></returns>
        string GetFrom();

        /// <summary>
        /// 获取Join语句
        /// </summary>
        /// <returns></returns>
        string GetJoin();

        /// <summary>
        /// 获取Where语句
        /// </summary>
        /// <returns></returns>
        string GetWhere();

        /// <summary>
        /// 获取分组语句
        /// </summary>
        /// <returns></returns>
        string GetGroupBy();

        /// <summary>
        /// 获取排序语句
        /// </summary>
        /// <returns></returns>
        string GetOrderBy();

        /// <summary>
        /// 生成调试Sql语句，Sql语句中的参数被替换为参数值
        /// </summary>
        /// <returns></returns>
        string ToDebugSql();

        /// <summary>
        /// 生成Sql语句
        /// </summary>
        /// <returns></returns>
        string ToSql();
    }
}
