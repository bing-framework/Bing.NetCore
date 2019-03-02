using System.Threading.Tasks;

namespace Bing.Biz.OAuthLogin.QQ.Configs
{
    /// <summary>
    /// QQ授权配置提供器
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public interface IQQAuthorizationConfigProvider
    {
        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        Task<QQAuthorizationConfig> GetConfigAsync();
    }
}
