namespace Bing.Exceptions;

/// <summary>
/// Bing框架异常选项配置
/// </summary>
public class BingExceptionOptions
{
    /// <summary>
    /// 空标识
    /// </summary>
    protected const string EmptyFlag = "__EMPTY_FLG";

    /// <summary>
    /// 默认错误消息
    /// </summary>
    protected const string DefaultErrorMessage = "_DEFAULT_ERROR";

    /// <summary>
    /// 默认扩展错误编码
    /// </summary>
    protected const long DefaultExtendErrorCode = 1002;

    /// <summary>
    /// 错误消息。默认值：<see cref="DefaultErrorMessage"/>
    /// </summary>
    public string Message { get; set; } = DefaultErrorMessage;

    /// <summary>
    /// 错误标识。默认值：<see cref="EmptyFlag"/>
    /// </summary>
    public string Flag { get; set; } = EmptyFlag;

    /// <summary>
    /// 错误码。默认值：<see cref="DefaultExtendErrorCode"/>
    /// </summary>
    public long ErrorCode { get; set; } = DefaultExtendErrorCode;

    /// <summary>
    /// 内部异常
    /// </summary>
    public Exception InnerException { get; set; }

    /// <summary>
    /// 额外错误
    /// </summary>
    public Dictionary<string, object> ExtraErrors { get; set; } = new Dictionary<string, object>();
}
