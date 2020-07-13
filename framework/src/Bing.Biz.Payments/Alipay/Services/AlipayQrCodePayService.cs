using System.Threading.Tasks;
using Bing.Biz.Payments.Alipay.Abstractions;
using Bing.Biz.Payments.Alipay.Configs;
using Bing.Biz.Payments.Alipay.Parameters;
using Bing.Biz.Payments.Alipay.Parameters.Requests;
using Bing.Biz.Payments.Alipay.Results;
using Bing.Biz.Payments.Alipay.Services.Base;
using Bing.Biz.Payments.Core;

namespace Bing.Biz.Payments.Alipay.Services
{
    /// <summary>
    /// 支付宝二维码支付服务
    /// </summary>
    public class AlipayQrCodePayService : AlipayServiceBase, IAlipayQrCodePayService
    {
        /// <summary>
        /// 初始化一个<see cref="AlipayQrCodePayService"/>类型的实例
        /// </summary>
        /// <param name="provider">支付宝配置提供器</param>
        public AlipayQrCodePayService(IAlipayConfigProvider provider) : base(provider)
        {
        }

        /// <summary>
        /// 支付
        /// </summary>
        /// <param name="request">条码支付参数</param>
        /// <returns></returns>
        public async Task<string> PayAsync(AlipayPrecreateRequest request)
        {
            var result = await PayAsync(request.ToParam());
            return result.Result;
        }

        /// <summary>
        /// 获取请求方法
        /// </summary>
        /// <returns></returns>
        protected override string GetMethod()
        {
            return "alipay.trade.precreate";
        }

        /// <summary>
        /// 获取支付方式
        /// </summary>
        /// <returns></returns>
        protected override PayWay GetPayWay()
        {
            return PayWay.AlipayQrCodePay;
        }

        /// <summary>
        /// 创建结果
        /// </summary>
        /// <param name="builder">支付宝参数生成器</param>
        /// <param name="result">支付宝结果</param>
        /// <returns></returns>
        protected override PayResult CreateResult(AlipayParameterBuilder builder, AlipayResult result)
        {
            return new PayResult(result.Success, result.GetTradeNo(), result.Raw)
            {
                Parameter = builder.ToString(),
                Message = result.GetMessage(),
                Result = result.GetValue(AlipayConst.QrCode)
            };
        }
    }
}
