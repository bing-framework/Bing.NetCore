using System.Threading.Tasks;
using Bing.Biz.Payments.Core;
using Bing.Biz.Payments.Wechatpay.Abstractions;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Parameters;
using Bing.Biz.Payments.Wechatpay.Parameters.Requests;
using Bing.Biz.Payments.Wechatpay.Services.Base;
using Bing.Helpers;

namespace Bing.Biz.Payments.Wechatpay.Services
{
    /// <summary>
    /// 微信退款服务
    /// </summary>
    public class WechatpayRefundService : WechatpayServiceBase<WechatRefundRequest, WechatpayRefundParameterBuilder>, IWechatpayRefundService
    {
        /// <summary>
        /// 初始化一个<see cref="WechatpayRefundService"/>类型的实例
        /// </summary>
        /// <param name="configProvider">微信支付配置提供程序</param>
        public WechatpayRefundService(IWechatpayConfigProvider configProvider) : base(configProvider)
        {
        }

        /// <summary>
        /// 退款
        /// </summary>
        /// <param name="request">退款参数</param>
        public Task<RefundResult> RefundAsync(WechatRefundRequest request)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// 创建参数生成器
        /// </summary>
        /// <param name="config">微信支付配置</param>
        protected override WechatpayRefundParameterBuilder CreateParameterBuilder(WechatpayConfig config) => new WechatpayRefundParameterBuilder(config);

        /// <summary>
        /// 配置参数生成器
        /// </summary>
        /// <param name="builder">微信支付参数生成器</param>
        /// <param name="param">请求参数</param>
        protected override void ConfigBuilder(WechatpayRefundParameterBuilder builder, WechatRefundRequest param)
        {
            builder.Init(param);
            builder.Add(WechatpayConst.AppId, builder.Config.AppId)
                .Add(WechatpayConst.MerchantId, builder.Config.MerchantId)
                .Add(WechatpayConst.NonceStr, Id.Guid());
        }

        /// <summary>
        /// 获取接口地址
        /// </summary>
        /// <param name="config">微信支付配置</param>
        protected override string GetUrl(WechatpayConfig config) => config.GetRefundUrl();
    }
}
