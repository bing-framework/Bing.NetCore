// ReSharper disable once CheckNamespace
namespace Bing.Encryption
{
    /// <summary>
    /// AES 密钥
    /// </summary>
    public class AESKey
    {
        /// <summary>
        /// 密钥
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 偏移量
        /// </summary>
        public string IV { get; set; }

        /// <summary>
        /// 密钥长度
        /// </summary>
        public AESKeySizeType Size { get; set; }
    }
}
