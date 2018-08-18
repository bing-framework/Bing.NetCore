namespace Bing.Datas.Sql.Queries.Builders.Abstractions
{
    /// <summary>
    /// 分组子句
    /// </summary>
    public interface IGroupByClause
    {
        /// <summary>
        /// 分组
        /// </summary>
        /// <param name="groupBy">分组列表</param>
        void GroupBy(string groupBy);

        /// <summary>
        /// 添加到GroupBy子句
        /// </summary>
        /// <param name="groupBy">分组列表</param>
        void AppendGroupBy(string groupBy);

        /// <summary>
        /// 获取Sql
        /// </summary>
        /// <returns></returns>
        string ToSql();
    }
}
