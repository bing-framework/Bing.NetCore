using System;
using Bing.Payments.Gateways;
using Bing.Payments.Notify;
using Bing.Payments.Response;
using Bing.Utils.Helpers;

namespace Bing.Payments.Events
{
    /// <summary>
    /// 通知事件参数基类
    /// </summary>
    public abstract class NotifyEventArgs:EventArgs
    {
        /// <summary>
        /// 网关
        /// </summary>
        protected GatewayBase _gateway;

        /// <summary>
        /// 通知服务主机地址
        /// </summary>
        private readonly string _notifyServerHostAddress;

        /// <summary>
        /// 发送支付通知的网关IP地址
        /// </summary>
        public string NotifyServerHostAddress => _notifyServerHostAddress;

        /// <summary>
        /// 网关数据
        /// </summary>
        public GatewayData GatewayData => _gateway.GatewayData;

        /// <summary>
        /// 网关类型
        /// </summary>
        public Type GatewayType => _gateway.GetType();

        /// <summary>
        /// 通知数据
        /// </summary>
        public IResponse NotifyResponse => _gateway.NotifyResponse;

        /// <summary>
        /// 通知类型
        /// </summary>
        public NotifyType NotifyType => Web.RequestType == "GET" ? NotifyType.Sync : NotifyType.Async;

        /// <summary>
        /// 初始化一个<see cref="NotifyEventArgs"/>类型的实例
        /// </summary>
        /// <param name="gateway">网关</param>
        protected NotifyEventArgs(GatewayBase gateway)
        {
            _gateway = gateway;
            _notifyServerHostAddress = Web.IP;
        }
    }
}
