using Bing.Datas.Sql.Queries.Builders.Abstractions;

namespace Bing.Datas.Sql.Queries
{
    /// <summary>
    /// Sql查询对象
    /// </summary>
    public interface ISqlQuery
    {
        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        /// <returns></returns>
        ISqlBuilder NewBuilder();
    }
}
