namespace Bing.Payments
{
    /// <summary>
    /// 商户
    /// </summary>
    public interface IMerchant
    {
        /// <summary>
        /// 商户标识
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// 应用ID
        /// </summary>
        string AppId { get; set; }

        /// <summary>
        /// 签名类型
        /// </summary>
        string SignType { get; set; }

        /// <summary>
        /// 网关回发通知URL
        /// </summary>
        string NotifyUrl { get; set; }
    }
}
