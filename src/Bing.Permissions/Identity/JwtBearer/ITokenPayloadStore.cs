using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bing.Permissions.Identity.JwtBearer
{
    /// <summary>
    /// 令牌Payload存储器
    /// </summary>
    public interface ITokenPayloadStore
    {
        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="token">令牌</param>
        /// <param name="payload">负载字典</param>
        /// <param name="expires">过期时间</param>
        Task SaveAsync(string token, IDictionary<string, string> payload, DateTime expires);

        /// <summary>
        /// 移除
        /// </summary>
        /// <param name="token">令牌</param>
        Task RemoveAsync(string token);

        /// <summary>
        /// 获取Payload
        /// </summary>
        /// <param name="token">令牌</param>
        Task<IDictionary<string, string>> GetAsync(string token);
    }
}
