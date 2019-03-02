using System.ComponentModel;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// OAuth方式
    /// </summary>
    public enum OAuthWay
    {
        /// <summary>
        /// QQ
        /// </summary>
        [Description("QQ")]
        // ReSharper disable once InconsistentNaming
        QQ,
        /// <summary>
        /// 微信
        /// </summary>
        [Description("微信")]
        Wechat,
        /// <summary>
        /// 新浪微博
        /// </summary>
        [Description("新浪微博")]
        Weibo,
        /// <summary>
        /// 淘宝
        /// </summary>
        [Description("淘宝")]
        Taobao,
        /// <summary>
        /// 微软
        /// </summary>
        [Description("微软")]
        Microsoft,
        /// <summary>
        /// Github
        /// </summary>
        [Description("Github")]
        Github,
        /// <summary>
        /// Facebook
        /// </summary>
        [Description("Facebook")]
        Facebook,
        /// <summary>
        /// Kakao
        /// </summary>
        [Description("Kakao")]
        Kakao,
        /// <summary>
        /// 有赞
        /// </summary>
        [Description("有赞")]
        Youzan,
        /// <summary>
        /// 京东
        /// </summary>
        [Description("京东")]
        Jd,
        /// <summary>
        /// 阿里巴巴
        /// </summary>
        [Description("阿里巴巴")]
        Alibaba,
        /// <summary>
        /// 美丽说
        /// </summary>
        [Description("美丽说")]
        MeiliShuo,
    }
}
