namespace Bing;

/// <summary>
/// Bing框架异常
/// </summary>
public class BingFrameworkException : BingException
{
    /// <summary>
    /// 标识
    /// </summary>
    private const string FLAG = "__BING_FRM_FLG";

    /// <summary>
    /// 默认错误消息
    /// </summary>
    private const string DEFAULT_ERROR_MSG = "Bing框架内部异常";

    /// <summary>
    /// 默认错误码
    /// </summary>
    private const long ERROR_CODE = 1003;

    /// <summary>
    /// 初始化一个<see cref="BingFrameworkException"/>类型的实例
    /// </summary>
    public BingFrameworkException() : this(DEFAULT_ERROR_MSG) { }

    /// <summary>
    /// 初始化一个<see cref="BingFrameworkException"/>类型的实例
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    public BingFrameworkException(string errorMessage) 
        : this(ERROR_CODE, errorMessage)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="BingFrameworkException"/>类型的实例
    /// </summary>
    /// <param name="errorCode">错误码</param>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public BingFrameworkException(long errorCode, string errorMessage, Exception innerException = null) 
        : base(errorCode, errorMessage, FLAG, innerException)
    {
    }
}
