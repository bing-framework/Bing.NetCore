using System.Collections.Generic;

namespace Bing.Datas.Sql.Builders.Filters
{
    /// <summary>
    /// Sql过滤器集合
    /// </summary>
    public static class SqlFilterCollection
    {
        /// <summary>
        /// Sql过滤器集合
        /// </summary>
        public static List<ISqlFilter> Filters { get; }

        /// <summary>
        /// 初始化一个<see cref="SqlFilterCollection"/>类型的静态实例
        /// </summary>
        static SqlFilterCollection() => Filters = new List<ISqlFilter>() {new IsDeletedFilter()};

        /// <summary>
        /// 添加Sql过滤器
        /// </summary>
        /// <param name="filter">Sql查询过滤器</param>
        public static void Add(ISqlFilter filter)
        {
            if (filter == null)
                return;
            Filters.Add(filter);
        }
    }
}
