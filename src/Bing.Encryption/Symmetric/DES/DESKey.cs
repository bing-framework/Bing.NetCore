// ReSharper disable once CheckNamespace
namespace Bing.Encryption
{
    /// <summary>
    /// DES 密钥
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class DESKey
    {
        /// <summary>
        /// 密钥
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 偏移量
        /// </summary>
        public string IV { get; set; }
    }
}
