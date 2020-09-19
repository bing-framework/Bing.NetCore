using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Validations;

namespace Bing.Biz.Payments.Alipay.Abstractions
{
    /// <summary>
    /// 支付宝返回服务
    /// </summary>
    public interface IAlipayReturnService
    {
        /// <summary>
        /// 商户订单号
        /// </summary>
        string OrderId { get; }

        /// <summary>
        /// 支付订单号
        /// </summary>
        string TradeNo { get; }

        /// <summary>
        /// 支付金额
        /// </summary>
        decimal Money { get; }

        /// <summary>
        /// 买家支付宝用户号
        /// </summary>
        string BuyerId { get; }

        /// <summary>
        /// 获取参数
        /// </summary>
        /// <param name="name">参数名</param>
        /// <returns></returns>
        string GetParam(string name);

        /// <summary>
        /// 获取参数
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="name">参数名</param>
        /// <returns></returns>
        T GetParam<T>(string name);

        /// <summary>
        /// 获取参数集合
        /// </summary>
        /// <returns></returns>
        IDictionary<string, string> GetParams();

        /// <summary>
        /// 验证
        /// </summary>
        /// <returns></returns>
        Task<ValidationResultCollection> ValidateAsync();
    }
}
