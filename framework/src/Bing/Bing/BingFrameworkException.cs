namespace Bing;

/// <summary>
/// Bing框架异常
/// </summary>
public class BingFrameworkException : BingException
{
    /// <summary>
    /// 框架标识
    /// </summary>
    private const string BingFrameworkFlag = "BING_FRM_FLG";

    /// <summary>
    /// 框架内部消息
    /// </summary>
    private const string FrameworkInnerMessage = "Bing框架内部异常";

    /// <summary>
    /// 初始化一个<see cref="BingFrameworkException"/>类型的实例
    /// </summary>
    public BingFrameworkException() : this(FrameworkInnerMessage) { }

    /// <summary>
    /// 初始化一个<see cref="BingFrameworkException"/>类型的实例
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    public BingFrameworkException(string errorMessage) : this(DefaultErrorCode, errorMessage)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="BingFrameworkException"/>类型的实例
    /// </summary>
    /// <param name="errorCode">错误码</param>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="innerException">内部异常</param>
    public BingFrameworkException(long errorCode, string errorMessage, Exception innerException = null) : base(errorCode, errorMessage, BingFrameworkFlag, innerException)
    {
    }
}
