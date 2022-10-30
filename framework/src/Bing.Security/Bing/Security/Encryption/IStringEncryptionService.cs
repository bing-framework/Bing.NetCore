namespace Bing.Security.Encryption;

/// <summary>
/// 字符串加密服务
/// </summary>
public interface IStringEncryptionService
{
    /// <summary>
    /// 加密
    /// </summary>
    /// <param name="plainText">明文</param>
    /// <param name="passPhrase">密码</param>
    /// <param name="salt">盐</param>
    string Encrypt(string plainText, string passPhrase = null, byte[] salt = null);

    /// <summary>
    /// 解密
    /// </summary>
    /// <param name="cipherText">密文</param>
    /// <param name="passPhrase">密码</param>
    /// <param name="salt">盐</param>
    string Decrypt(string cipherText, string passPhrase = null, byte[] salt = null);
}