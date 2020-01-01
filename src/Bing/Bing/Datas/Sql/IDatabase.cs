using System.Data;
using Bing.Aspects;

namespace Bing.Datas.Sql
{
    /// <summary>
    /// 数据库
    /// </summary>
    [Ignore]
    public interface IDatabase
    {
        /// <summary>
        /// 获取数据库连接
        /// </summary>
        /// <returns></returns>
        IDbConnection GetConnection();
    }
}
