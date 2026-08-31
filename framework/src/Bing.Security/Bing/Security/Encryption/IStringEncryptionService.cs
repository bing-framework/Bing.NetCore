namespace Bing.Security.Encryption;

/// <summary>
/// 定义使用可选口令和盐处理字符串的加密服务。
/// </summary>
public interface IStringEncryptionService
{
    /// <summary>
    /// 将明文字符串加密为实现定义的密文表示。
    /// </summary>
    /// <param name="plainText">要加密的明文。</param>
    /// <param name="passPhrase">可选的加密口令；未提供时由实现决定默认处理方式。</param>
    /// <param name="salt">可选的盐字节；未提供时由实现决定默认处理方式。</param>
    /// <returns>加密后的密文表示。</returns>
    /// <remarks>调用方不应记录明文、口令、盐或密文中的敏感内容。</remarks>
    string Encrypt(string plainText, string passPhrase = null, byte[] salt = null);

    /// <summary>
    /// 将密文字符串解密为原始明文表示。
    /// </summary>
    /// <param name="cipherText">要解密的密文。</param>
    /// <param name="passPhrase">可选的解密口令；未提供时由实现决定默认处理方式。</param>
    /// <param name="salt">可选的盐字节；未提供时由实现决定默认处理方式。</param>
    /// <returns>解密后的明文表示。</returns>
    /// <remarks>调用方不应记录口令、盐、密文或解密后的敏感明文。</remarks>
    string Decrypt(string cipherText, string passPhrase = null, byte[] salt = null);
}