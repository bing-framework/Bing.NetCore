using System.Threading.Tasks;
using Bing.Biz.Payments.Alipay.Abstractions;
using Bing.Biz.Payments.Alipay.Configs;
using Bing.Biz.Payments.Alipay.Parameters;
using Bing.Biz.Payments.Alipay.Parameters.Requests;
using Bing.Biz.Payments.Alipay.Services.Base;
using Bing.Biz.Payments.Core;
using Bing.Helpers;
using Bing.Utils.Helpers;

namespace Bing.Biz.Payments.Alipay.Services
{
    /// <summary>
    /// 支付宝电脑网站支付服务
    /// </summary>
    public class AlipayPagePayService : AlipayServiceBase, IAlipayPagePayService
    {
        /// <summary>
        /// 初始化一个<see cref="AlipayPagePayService"/>类型的实例
        /// </summary>
        /// <param name="provider">支付宝配置提供器</param>
        public AlipayPagePayService(IAlipayConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 支付，返回跳转地址
        /// </summary>
        /// <param name="request">支付宝电脑网站支付参数</param>
        /// <returns></returns>
        public async Task<string> PayAsync(AlipayPagePayRequest request)
        {
            var result = await PayAsync(request.ToParam());
            return result.Result;
        }

        /// <summary>
        /// 支付，跳转到支付宝收银台
        /// </summary>
        /// <param name="request">支付宝电脑网站支付参数</param>
        /// <returns></returns>
        public async Task RedirectAsync(AlipayPagePayRequest request)
        {
            var result = await PayAsync(request);
            var response = Web.Response;
            var config = await ConfigProvider.GetConfigAsync();
            response.Redirect($"{config.GatewayUrl}?{result}");
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
            return "alipay.trade.page.pay";
        }

        /// <summary>
        /// 获取支付方式
        /// </summary>
        /// <returns></returns>
        protected override PayWay GetPayWay()
        {
            return PayWay.AlipayPagePay;
        }

        /// <summary>
        /// 初始化内容生成器
        /// </summary>
        /// <param name="builder">内容参数生成器</param>
        /// <param name="param">支付参数</param>
        protected override void InitContentBuilder(AlipayContentBuilder builder, PayParam param)
        {
            builder.ProductCode("FAST_INSTANT_TRADE_PAY");
        }
    }
}
