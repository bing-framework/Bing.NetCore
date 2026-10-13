using Bing.Properties;

namespace Bing.Exceptions;

/// <summary>
/// 表示持久化操作因并发冲突而未能完成的异常。
/// </summary>
public class ConcurrencyException : Warning
{
    /// <summary>
    /// 保存附加的并发冲突消息，用于组合最终错误信息。
    /// </summary>
    private readonly string _message;

    /// <summary>
    /// 初始化一个 <see cref="ConcurrencyException"/> 类型的实例。
    /// </summary>
    public ConcurrencyException() : this("")
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="ConcurrencyException"/> 类型的实例。
    /// </summary>
    /// <param name="message">并发冲突的附加消息。</param>
    public ConcurrencyException(string message) : this(message, null)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="ConcurrencyException"/> 类型的实例。
    /// </summary>
    /// <param name="exception">导致当前并发异常的内部异常。</param>
    public ConcurrencyException(Exception exception) : this("", exception)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="ConcurrencyException"/> 类型的实例。
    /// </summary>
    /// <param name="message">并发冲突的附加消息。</param>
    /// <param name="exception">导致当前并发异常的内部异常。</param>
    public ConcurrencyException(string message, Exception exception) : this(message, exception, "")
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="ConcurrencyException"/> 类型的实例。
    /// </summary>
    /// <param name="message">并发冲突的附加消息。</param>
    /// <param name="exception">导致当前并发异常的内部异常。</param>
    /// <param name="code">用于分类该异常的错误码。</param>
    public ConcurrencyException(string message, Exception exception, string code) : base(message, code, exception)
    {
        _message = message;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 获取包含并发冲突资源文本和附加消息的错误消息。
    /// </remarks>
    public override string Message => $"{LibraryResource.ConcurrencyExceptionMessage}.{_message}";

    /// <inheritdoc />
    /// <remarks>
    /// 根据运行环境获取并发异常的展示消息。
    /// </remarks>
    public override string GetMessage(bool isProduction = true) => isProduction ? LibraryResource.ConcurrencyExceptionMessage : GetMessage(this);
}
