using System.Collections.Generic;
using System.Linq;
using Bing.Payments.Exceptions;

namespace Bing.Payments.Gateways
{
    /// <summary>
    /// 网关容器
    /// </summary>
    public class GatewayContoiner:IGatewayContoiner
    {
        /// <summary>
        /// 网关集合
        /// </summary>
        private readonly ICollection<GatewayBase> _list;

        /// <summary>
        /// 网关数量
        /// </summary>
        private int Count => _list.Count;

        /// <summary>
        /// 初始化一个<see cref="GatewayContoiner"/>类型的实例
        /// </summary>
        public GatewayContoiner()
        {
            _list = new List<GatewayBase>();
        }

        /// <summary>
        /// 添加网关
        /// </summary>
        /// <param name="gateway">网关</param>
        /// <returns></returns>
        public bool Add(GatewayBase gateway)
        {
            if (gateway == null)
            {
                return false;
            }
            if (Exist(gateway.Merchant.Id))
            {
                throw new GatewayException("该商户标识已存在");
            }
            _list.Add(gateway);
            return true;
        }

        /// <summary>
        /// 指定标识是否存在
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns></returns>
        private bool Exist(string id) => _list.Any(x => x.Merchant.Id == id);

        /// <summary>
        /// 获取指定网关
        /// </summary>
        /// <typeparam name="T">网关类型</typeparam>
        /// <returns></returns>
        public GatewayBase Get<T>()
        {
            var gatewayList = _list.Where(x => x is T).ToList();
            return gatewayList.Count > 0 ? gatewayList[0] : throw new GatewayException("找不到指定网关");
        }

        /// <summary>
        /// 通过AppId获取网关
        /// </summary>
        /// <typeparam name="T">网关类型</typeparam>
        /// <param name="appId">AppId</param>
        /// <returns></returns>
        public GatewayBase Get<T>(string appId)
        {
            var gatewayList = _list.Where(x => x is T && x.Merchant.AppId == appId).ToList();
            return gatewayList.Count > 0 ? gatewayList[0] : throw new GatewayException("找不到指定网关");
        }

        /// <summary>
        /// 通过标识获取网关
        /// </summary>
        /// <typeparam name="T">网关类型</typeparam>
        /// <param name="id">标识</param>
        /// <returns></returns>
        public GatewayBase GetById<T>(string id)
        {
            var gatewayList = _list.Where(x => x is T && x.Merchant.Id == id).ToList();
            return gatewayList.Count > 0 ? gatewayList[0] : throw new GatewayException("找不到指定网关");
        }

        /// <summary>
        /// 获取网关列表
        /// </summary>
        /// <returns></returns>
        public ICollection<GatewayBase> GetList()
        {
            return _list;
        }
    }
}
