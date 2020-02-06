namespace Bing.Tools.ExpressDelivery.Kdniao.Configuration
{
    /// <summary>
    /// 快递鸟账户
    /// </summary>
    public class KdniaoAccount : IAccountSettings
    {
        /// <summary>
        /// 商户ID
        /// </summary>
        public string MerchantId { get; set; }

        /// <summary>
        /// API Key
        /// </summary>
        public string ApiKey { get; set; }
    }
}
