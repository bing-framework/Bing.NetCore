using System.Threading.Tasks;
using Bing.Utils.Parameters;

namespace Bing.Biz.Payments.Wechatpay.Configs
{
    /// <summary>
    /// 微信支付配置提供器
    /// </summary>
    public interface IWechatpayConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <param name="parameterManager">参数管理器</param>
        /// <returns></returns>
        Task<WechatpayConfig> GetConfigAsync(IParameterManager parameterManager = null);
    }
}
