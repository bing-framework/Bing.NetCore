namespace Bing.ExceptionHandling;

/// <summary>
/// 错误码
/// </summary>
public interface IHasErrorCode
{
    /// <summary>
    /// 获取错误码
    /// </summary>
    string Code { get; }
}
