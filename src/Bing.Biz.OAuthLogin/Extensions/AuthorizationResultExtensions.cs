using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Wechat;
using Bing.Biz.OAuthLogin.Weibo;
using Bing.Utils.Extensions;
using Bing.Utils.Json;

namespace Bing.Biz.OAuthLogin.Extensions
{
    /// <summary>
    /// 授权结果(<see cref="AuthorizationResult"/>) 扩展
    /// </summary>
    public static class AuthorizationResultExtensions
    {
        /// <summary>
        /// 转换为QQ访问令牌
        /// </summary>
        /// <param name="value">授权结果</param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public static AccessTokenResult ToQQToken(this AuthorizationResult value)
        {
            value.CheckNotNull(nameof(value));
            return !value.Success ? null : value.Result.ToObject<AccessTokenResult>();
        }

        /// <summary>
        /// 转换为微信访问令牌
        /// </summary>
        /// <param name="value">授权结果</param>
        /// <returns></returns>
        public static WechatAccessTokenResult ToWechatToken(this AuthorizationResult value)
        {
            value.CheckNotNull(nameof(value));
            return !value.Success ? null : value.Result.ToObject<WechatAccessTokenResult>();
        }

        /// <summary>
        /// 转换为微博访问令牌
        /// </summary>
        /// <param name="value">授权结果</param>
        /// <returns></returns>
        public static WeiboAccessTokenResult ToWeiboToken(this AuthorizationResult value)
        {
            value.CheckNotNull(nameof(value));
            return !value.Success ? null : value.Result.ToObject<WeiboAccessTokenResult>();
        }
    }
}
