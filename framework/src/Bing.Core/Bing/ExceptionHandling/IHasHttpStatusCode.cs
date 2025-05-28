namespace Bing.ExceptionHandling;

/// <summary>
/// Http状态码
/// </summary>
public interface IHasHttpStatusCode
{
    /// <summary>
    /// Http状态码
    /// </summary>
    int HttpStatusCode { get; }
}
