// ReSharper disable once CheckNamespace
namespace Bing.Encryption
{
    /// <summary>
    /// RSA 密钥
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class RSAKey
    {
        /// <summary>
        /// 公钥
        /// </summary>
        public string PublickKey { get; set; }

        /// <summary>
        /// 私钥
        /// </summary>
        public string PrivateKey { get; set; }

        /// <summary>
        /// 公钥 指数
        /// </summary>
        public string Exponent { get; set; }

        /// <summary>
        /// 公钥 系数
        /// </summary>
        public string Modulus { get; set; }
    }
}
