namespace Bing.Datas.Sql.Queries.Builders.Operations
{
    /// <summary>
    /// 清空
    /// </summary>
    public interface IClear
    {
        /// <summary>
        /// 清空并初始化
        /// </summary>
        void Clear();

        /// <summary>
        /// 清空Select子句
        /// </summary>
        void ClearSelect();

        /// <summary>
        /// 清空From子句
        /// </summary>
        void ClearFrom();

        /// <summary>
        /// 清空Join子句
        /// </summary>
        void ClearJoin();

        /// <summary>
        /// 清空Where子句
        /// </summary>
        void ClearWhere();

        /// <summary>
        /// 清空GroupBy子句
        /// </summary>
        void ClearGroupBy();

        /// <summary>
        /// 清空OrderBy子句
        /// </summary>
        void ClearOrderBy();

        /// <summary>
        /// 清空Sql参数
        /// </summary>
        void ClearSqlParams();

        /// <summary>
        /// 清空分页参数
        /// </summary>
        void ClearPageParams();
    }
}
