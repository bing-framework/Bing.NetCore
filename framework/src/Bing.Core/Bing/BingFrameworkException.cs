namespace Bing;

/// <summary>
/// 表示 Bing 框架内部处理失败的异常。
/// </summary>
public class BingFrameworkException : BingException
{
    /// <summary>
    /// Bing 框架异常使用的内部标识。
    /// </summary>
    private const string FLAG = "__BING_FRM_FLG";

    /// <summary>
    /// 未指定异常消息时使用的默认错误消息。
    /// </summary>
    private const string DEFAULT_ERROR_MSG = "Bing框架内部异常";

    /// <summary>
    /// 未指定异常错误码时使用的默认错误码。
    /// </summary>
    private const long ERROR_CODE = 1003;

    /// <summary>
    /// 初始化一个 <see cref="BingFrameworkException"/> 类型的实例。
    /// </summary>
    public BingFrameworkException() : this(DEFAULT_ERROR_MSG) { }

    /// <summary>
    /// 初始化一个 <see cref="BingFrameworkException"/> 类型的实例。
    /// </summary>
    /// <param name="errorMessage">描述框架内部错误的消息。</param>
    public BingFrameworkException(string errorMessage) 
        : this(ERROR_CODE, errorMessage)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="BingFrameworkException"/> 类型的实例。
    /// </summary>
    /// <param name="errorCode">框架或业务约定的错误码。</param>
    /// <param name="errorMessage">描述错误的消息。</param>
    /// <param name="innerException">导致当前异常的内部异常，可为空。</param>
    public BingFrameworkException(long errorCode, string errorMessage, Exception innerException = null) 
        : base(errorCode, errorMessage, FLAG, innerException)
    {
    }
}
