using Bing.Security.Encryptors;

namespace Bing.Admin.Infrastructure.Encryptor
{
    /// <summary>
    /// AES算法 加密器
    /// </summary>
    public class AesEncryptor : IEncryptor
    {

        /// <summary>
        /// AES算法加密器实例
        /// </summary>
        public static readonly IEncryptor Instance = new AesEncryptor();

        /// <summary>
        /// 加密
        /// </summary>
        /// <param name="data">原始数据</param>
        public string Encrypt(string data) => Bing.Helpers.Encrypt.AesEncrypt(data);

        /// <summary>
        /// 解密
        /// </summary>
        /// <param name="data">已加密数据</param>
        public string Decrypt(string data) => Bing.Helpers.Encrypt.AesDecrypt(data);
    }
}
