namespace Bing.ExceptionHandling;

/// <summary>
/// 错误详情
/// </summary>
public interface IHasErrorDetails
{
    /// <summary>
    /// 获取错误详情
    /// </summary>
    string Details { get; }
}
