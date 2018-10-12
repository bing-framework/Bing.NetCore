using System.Collections.Generic;
using StackExchange.Redis;

namespace Bing.Caching.Redis
{
    /// <summary>
    /// Redis 数据库提供程序
    /// </summary>
    public interface IRedisDatabaseProvider
    {
        /// <summary>
        /// 数据库提供程序名称
        /// </summary>
        string DbProviderName { get; }

        /// <summary>
        /// 获取数据库
        /// </summary>
        /// <returns></returns>
        IDatabase GetDatabase();

        /// <summary>
        /// 获取服务器列表
        /// </summary>
        /// <returns></returns>
        IEnumerable<IServer> GetServerList();
    }
}
