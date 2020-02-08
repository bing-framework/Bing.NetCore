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

        /// <summary>
        /// 百度
        /// </summary>
        [Description("百度")]
        Baidu,

        /// <summary>
        /// Coding.NET
        /// </summary>
        [Description("Coding.NET")]
        Coding,

        /// <summary>
        /// 码云
        /// </summary>
        [Description("码云")]
        Gitee,

        /// <summary>
        /// 开源中国
        /// </summary>
        [Description("开源中国")]
        OsChina,

        /// <summary>
        /// 钉钉
        /// </summary>
        [Description("钉钉")]
        DingTalk,
    }
}
