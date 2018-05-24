namespace Bing.Security.Encryptors
{
    /// <summary>
    /// 加密器
    /// </summary>
    public interface IEncryptor
    {
        /// <summary>
        /// 加密
        /// </summary>
        /// <param name="data">原始数据</param>
        /// <returns></returns>
        string Encrypt(string data);

        /// <summary>
        /// 解密
        /// </summary>
        /// <param name="data">已加密数据</param>
        /// <returns></returns>
        string Decrypt(string data);
    }
}
