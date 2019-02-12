using Bing.Datas.Sql.Builders.Core;

namespace Bing.Datas.Sql.Builders
{
    /// <summary>
    /// Sql过滤器
    /// </summary>
    public interface ISqlFilter
    {
        /// <summary>
        /// 过滤
        /// </summary>
        /// <param name="context">Sql查询执行上下文</param>
        void Filter(SqlContext context);
    }
}
