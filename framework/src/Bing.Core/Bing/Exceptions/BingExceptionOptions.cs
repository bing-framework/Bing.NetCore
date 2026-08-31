namespace Bing.Exceptions;

/// <summary>
/// 配置 Bing 框架异常的默认消息、标识、错误码和附加错误信息。
/// </summary>
public class BingExceptionOptions
{
    /// <summary>
    /// 表示未设置异常标识时使用的内部占位值。
    /// </summary>
    protected const string EmptyFlag = "__EMPTY_FLG";

    /// <summary>
    /// 表示未设置异常消息时使用的内部默认键。
    /// </summary>
    protected const string DefaultErrorMessage = "_DEFAULT_ERROR";

    /// <summary>
    /// 表示未设置异常错误码时使用的默认扩展错误码。
    /// </summary>
    protected const long DefaultExtendErrorCode = 1002;

    /// <summary>
    /// 获取或设置异常消息或消息键，默认值为 <see cref="DefaultErrorMessage"/>。
    /// </summary>
    public string Message { get; set; } = DefaultErrorMessage;

    /// <summary>
    /// 获取或设置异常标识，默认值为 <see cref="EmptyFlag"/>。
    /// </summary>
    public string Flag { get; set; } = EmptyFlag;

    /// <summary>
    /// 获取或设置异常错误码，默认值为 <see cref="DefaultExtendErrorCode"/>。
    /// </summary>
    public long ErrorCode { get; set; } = DefaultExtendErrorCode;

    /// <summary>
    /// 获取或设置导致当前异常的内部异常。
    /// </summary>
    public Exception InnerException { get; set; }

    /// <summary>
    /// 获取或设置附加错误数据字典，用于携带主错误码之外的结构化信息。
    /// </summary>
    public Dictionary<string, object> ExtraErrors { get; set; } = new Dictionary<string, object>();
}
