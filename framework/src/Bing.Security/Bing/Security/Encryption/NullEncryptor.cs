namespace Bing.Security.Encryption;

/// <summary>
/// 不执行加密或解密操作的空对象加密器。
/// </summary>
public class NullEncryptor : IEncryptor
{
    /// <summary>
    /// 获取可复用的空对象加密器实例。
    /// </summary>
    public static readonly IEncryptor Instance = new NullEncryptor();

    /// <inheritdoc />
    /// <remarks>当前实现始终返回空字符串，不保留输入内容。</remarks>
    public string Encrypt(string data) => string.Empty;

    /// <inheritdoc />
    /// <remarks>当前实现始终返回空字符串，不还原输入内容。</remarks>
    public string Decrypt(string data) => string.Empty;
}