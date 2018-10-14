namespace Bing.Tools.ExpressDelivery.Kdniao.Configuration
{
    /// <summary>
    /// 快递鸟配置
    /// </summary>
    public class KdniaoConfig:IConfig
    {
        /// <summary>
        /// 快递鸟账户
        /// </summary>
        public KdniaoAccount Account { get; set; }

        /// <summary>
        /// 回调地址
        /// </summary>
        public string CallbackUrl { get; set; }

        /// <summary>
        /// 是否加密请求
        /// </summary>
        public bool Security { get; set; }

        /// <summary>
        /// 失败重试次数
        /// </summary>
        public int RetryTimes { get; set; } = 3;
    }
}
