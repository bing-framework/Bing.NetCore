using System.Threading.Tasks;
using Bing.Payments.Request;
using Bing.Payments.Response;
using Bing.Utils.Helpers;

namespace Bing.Payments.Gateways
{
    /// <summary>
    /// 网关基类
    /// </summary>
    public abstract class GatewayBase:IGateway
    {
        /// <summary>
        /// 商户数据
        /// </summary>
        public IMerchant Merchant { get; set; }        

        /// <summary>
        /// 通知数据
        /// </summary>
        public IResponse NotifyResponse { get; set; }

        /// <summary>
        /// 网关地址
        /// </summary>
        public abstract string GatewayUrl { get; set; }

        /// <summary>
        /// 网关数据
        /// </summary>
        protected internal GatewayData GatewayData { get; set; }

        /// <summary>
        /// 是否支付成功
        /// </summary>
        protected internal abstract bool IsPaySuccess { get; }

        /// <summary>
        /// 是否退款成功
        /// </summary>
        protected internal abstract bool IsRefundSuccess { get; }

        /// <summary>
        /// 是否撤销成功
        /// </summary>
        protected internal abstract bool IsCancelSuccess { get; }

        /// <summary>
        /// 需要验证的参数名称数组，用于识别不同的网关类型。
        /// 商户号(AppId)必须放第一位
        /// </summary>
        protected internal abstract string[] NotifyVerifyParameter { get; }

        /// <summary>
        /// 初始化一个<see cref="GatewayBase"/>类型的实例
        /// </summary>
        protected GatewayBase() { }

        /// <summary>
        /// 初始化一个<see cref="GatewayBase"/>类型的实例
        /// </summary>
        /// <param name="merchant">商户</param>
        protected GatewayBase(IMerchant merchant)
        {
            Merchant = merchant;
        }

        /// <summary>
        /// 校验网关返回的通知，确定订单是否支付成功
        /// </summary>
        /// <returns></returns>
        protected internal abstract Task<bool> ValidateNotifyAsync();

        /// <summary>
        /// 当接收到支付网关通知并验证无误时按照支付网关要求格式输出表示成功接收到网关通知的字符串
        /// </summary>
        protected internal virtual void WriteSuccessFlag()
        {
            Web.Write("success");
        }

        /// <summary>
        /// 当接收到支付网关通知并验证有误时按照支付网关要求格式输出表示失败接收到网关通知的字符串
        /// </summary>
        protected internal virtual void WriteFailureFlag()
        {
            Web.Write("failure");
        }

        /// <summary>
        /// 执行
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <typeparam name="TResponse">响应了洗ing</typeparam>
        /// <param name="request">请求数据</param>
        /// <returns></returns>
        public abstract TResponse Execute<TEntity, TResponse>(RequestBase<TEntity, TResponse> request) where TResponse : IResponse;
    }
}
