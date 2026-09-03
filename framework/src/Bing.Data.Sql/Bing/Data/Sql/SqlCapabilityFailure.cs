using System.ComponentModel;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 能力拒绝原因。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum SqlCapabilityFailureReason
{
    /// <summary>
    /// 当前数据库语义或版本不支持该能力。
    /// </summary>
    DatabaseUnsupported = 0,

    /// <summary>
    /// 数据库支持该能力，但 Bing Provider 尚未实现。
    /// </summary>
    ProviderImplementationGap = 1,

    /// <summary>
    /// Provider 未声明统一能力档案。
    /// </summary>
    ProviderProfileMissing = 2,

    /// <summary>
    /// Provider 能力声明与当前执行对象或上下文不一致。
    /// </summary>
    ProviderProfileMismatch = 3
}

/// <summary>
/// 为能力拒绝异常附加结构化原因的程序集内部运行时桥接。
/// </summary>
/// <remarks>
/// 保留现有 <see cref="NotSupportedException"/> 类型和消息；结构化信息存放在异常数据中，
/// 供框架内部和直接测试读取。
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SqlCapabilityFailure
{
    /// <summary>
    /// 异常数据中的能力拒绝原因键。
    /// </summary>
    private const string ReasonDataKey = "Bing.Data.Sql.CapabilityFailureReason";

    /// <summary>
    /// 异常数据中的能力名称键。
    /// </summary>
    private const string CapabilityDataKey = "Bing.Data.Sql.Capability";

    /// <summary>
    /// 异常数据中的 Provider Key 键。
    /// </summary>
    private const string ProviderKeyDataKey = "Bing.Data.Sql.ProviderKey";

    /// <summary>
    /// 创建带结构化能力拒绝原因的标准不支持异常。
    /// </summary>
    /// <param name="reason">能力拒绝原因。</param>
    /// <param name="capability">被拒绝的能力名称。</param>
    /// <param name="providerKey">Provider Key；不可用时可为空。</param>
    /// <param name="message">对现有调用方保持兼容的异常消息。</param>
    /// <returns>附带结构化原因的 <see cref="NotSupportedException"/>。</returns>
    public static NotSupportedException Create(SqlCapabilityFailureReason reason, string capability,
        string providerKey, string message)
    {
        var exception = new NotSupportedException(message);
        exception.Data[ReasonDataKey] = reason;
        if (string.IsNullOrWhiteSpace(capability) == false)
            exception.Data[CapabilityDataKey] = capability;
        if (string.IsNullOrWhiteSpace(providerKey) == false)
            exception.Data[ProviderKeyDataKey] = providerKey;
        return exception;
    }

    /// <summary>
    /// 读取异常中的结构化能力拒绝原因名称。
    /// </summary>
    /// <param name="exception">待读取的异常。</param>
    /// <returns>能力拒绝原因名称；未附加时返回 <see langword="null"/>。</returns>
    private static string GetReason(Exception exception) =>
        exception?.Data?[ReasonDataKey]?.ToString();

    /// <summary>
    /// 尝试读取异常中的结构化能力拒绝原因。
    /// </summary>
    /// <param name="exception">待读取的异常。</param>
    /// <param name="reason">读取到的能力拒绝原因。</param>
    /// <returns>包含有效原因时返回 <see langword="true"/>。</returns>
    public static bool TryGetReason(Exception exception, out SqlCapabilityFailureReason reason)
    {
        reason = default;
        if (Enum.TryParse(GetReason(exception), true, out reason) == false)
            return false;
        return Enum.IsDefined(typeof(SqlCapabilityFailureReason), reason);
    }
}
