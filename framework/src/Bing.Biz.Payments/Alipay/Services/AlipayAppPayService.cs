using System.Threading.Tasks;
using Bing.Biz.Payments.Alipay.Abstractions;
using Bing.Biz.Payments.Alipay.Configs;
using Bing.Biz.Payments.Alipay.Parameters;
using Bing.Biz.Payments.Alipay.Parameters.Requests;
using Bing.Biz.Payments.Alipay.Services.Base;
using Bing.Biz.Payments.Core;

namespace Bing.Biz.Payments.Alipay.Services
{
    /// <summary>
    /// 支付宝App支付服务
    /// </summary>
    public class AlipayAppPayService : AlipayServiceBase, IAlipayAppPayService
    {
        /// <summary>
        /// 初始化一个<see cref="AlipayAppPayService"/>类型的实例
        /// </summary>
        /// <param name="provider">支付宝配置提供器</param>
        public AlipayAppPayService(IAlipayConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 支付
        /// </summary>
        /// <param name="request">支付参数</param>
        /// <returns></returns>
        public async Task<string> PayAsync(AlipayAppPayRequest request)
        {
            var result = await PayAsync(request.ToParam());
            return result.Result;
        }

        /// <summary>
        /// 请求结果
        /// </summary>
        /// <param name="config">支付宝配置</param>
        /// <param name="builder">支付宝参数生成器</param>
        /// <returns></returns>
        protected override Task<PayResult> RequestResult(AlipayConfig config, AlipayParameterBuilder builder)
        {
            var result = builder.Result(true);
            WriteLog(config, builder, result);
            return Task.FromResult(new PayResult() { Result = result });
        }

        /// <summary>
        /// 获取请求方法
        /// </summary>
        /// <returns></returns>
        protected override string GetMethod()
        {
            return "alipay.trade.app.pay";
        }

        /// <summary>
        /// 获取支付方式
        /// </summary>
        /// <returns></returns>
        protected override PayWay GetPayWay()
        {
            return PayWay.AlipayAppPay;
        }
    }
}
