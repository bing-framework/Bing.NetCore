namespace Bing.Utils.Signatures
{
    /// <summary>
    /// 签名密钥
    /// </summary>
    public interface ISignKey
    {
        /// <summary>
        /// 获取私钥
        /// </summary>
        /// <returns></returns>
        string GetKey();

        /// <summary>
        /// 获取公钥
        /// </summary>
        /// <returns></returns>
        string GetPublicKey();
    }
}
