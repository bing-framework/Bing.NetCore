namespace Bing.Security.Encryption;

/// <summary>
/// 定义字符串数据的加密和解密能力。
/// </summary>
public interface IEncryptor
{
    /// <summary>
    /// 将原始字符串转换为实现定义的加密表示。
    /// </summary>
    /// <param name="data">要加密的原始字符串。</param>
    /// <returns>加密后的字符串表示。</returns>
    string Encrypt(string data);

    /// <summary>
    /// 将实现定义的加密字符串还原为原始表示。
    /// </summary>
    /// <param name="data">要解密的加密字符串。</param>
    /// <returns>解密后的原始字符串表示。</returns>
    string Decrypt(string data);
}