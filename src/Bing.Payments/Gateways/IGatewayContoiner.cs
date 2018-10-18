using System.Collections.Generic;

namespace Bing.Payments.Gateways
{
    /// <summary>
    /// 网关容器
    /// </summary>
    public interface IGatewayContoiner
    {
        /// <summary>
        /// 添加网关
        /// </summary>
        /// <param name="gateway">网关</param>
        /// <returns></returns>
        bool Add(GatewayBase gateway);

        /// <summary>
        /// 获取指定网关
        /// </summary>
        /// <typeparam name="T">网关类型</typeparam>
        /// <returns></returns>
        GatewayBase Get<T>();

        /// <summary>
        /// 通过AppId获取网关
        /// </summary>
        /// <typeparam name="T">网关类型</typeparam>
        /// <param name="appId">AppId</param>
        /// <returns></returns>
        GatewayBase Get<T>(string appId);

        /// <summary>
        /// 通过标识获取网关
        /// </summary>
        /// <typeparam name="T">网关类型</typeparam>
        /// <param name="id">标识</param>
        /// <returns></returns>
        GatewayBase GetById<T>(string id);

        /// <summary>
        /// 获取网关列表
        /// </summary>
        /// <returns></returns>
        ICollection<GatewayBase> GetList();
    }
}
