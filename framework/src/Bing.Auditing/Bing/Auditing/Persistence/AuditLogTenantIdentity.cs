using System;
using System.Text;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 提供审计租户标识的统一校验。
/// </summary>
internal static class AuditLogTenantIdentity
{
    /// <summary>
    /// 审计租户标识使用的严格 UTF-8 编码器。
    /// </summary>
    private static readonly Encoding Encoding = new UTF8Encoding(false, true);

    /// <summary>
    /// 校验并保留租户标识原值。
    /// </summary>
    /// <param name="value">原始租户标识。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <param name="required">是否要求标识非空。</param>
    /// <returns>通过校验的原始租户标识；非必填空值返回空字符串。</returns>
    /// <remarks>允许首尾空白，不执行修剪或 Unicode 规范化；孤立 UTF-16 代理字符会被拒绝。</remarks>
    public static string Validate(string value, string parameterName, bool required)
    {
        value = value ?? string.Empty;
        if (required && value.Length == 0)
            throw new ArgumentException("租户标识不能为空。", parameterName);
        if (value.Length > 128 || (value.Length > 0 && string.IsNullOrWhiteSpace(value)))
            throw new ArgumentException("租户标识不得超过 128 字符或全部为空白。", parameterName);

        Encoding.GetByteCount(value);
        return value;
    }
}
