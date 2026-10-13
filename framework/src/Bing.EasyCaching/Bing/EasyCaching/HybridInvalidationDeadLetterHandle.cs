using System.Security.Cryptography;
using System.Text;

namespace Bing.EasyCaching;

/// <summary>
/// 生成并校验 dead-letter 句柄和确认令牌。
/// </summary>
internal static class HybridInvalidationDeadLetterHandle
{
    /// <summary>
    /// 确认令牌使用的哈希域分隔前缀。
    /// </summary>
    private const string ConfirmationDomain = "bing-cache-dead-letter-confirmation\0";

    /// <summary>
    /// 根据 payload 创建不透明句柄。
    /// </summary>
    /// <param name="payload">dead-letter payload。</param>
    /// <returns>Base64 编码的句柄。</returns>
    internal static string Create(string payload) => Compute(payload);

    /// <summary>
    /// 根据 payload 创建删除确认令牌。
    /// </summary>
    /// <param name="payload">dead-letter payload。</param>
    /// <returns>Base64 编码的确认令牌。</returns>
    internal static string CreateConfirmationToken(string payload) =>
        Compute(ConfirmationDomain + payload);

    /// <summary>
    /// 校验句柄格式。
    /// </summary>
    /// <param name="handle">待校验的句柄。</param>
    /// <returns>句柄包含规范 Base64 编码的 SHA-256 值时返回 <see langword="true"/>。</returns>
    internal static bool IsValid(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
            return false;
        try
        {
            var bytes = Convert.FromBase64String(handle);
            return bytes.Length == 32 &&
                   string.Equals(Convert.ToBase64String(bytes), handle, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// 计算字符串的 SHA-256 摘要并编码为 Base64。
    /// </summary>
    /// <param name="value">待计算的字符串。</param>
    /// <returns>Base64 编码的摘要。</returns>
    private static string Compute(string value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        using (var sha = SHA256.Create())
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }
}
