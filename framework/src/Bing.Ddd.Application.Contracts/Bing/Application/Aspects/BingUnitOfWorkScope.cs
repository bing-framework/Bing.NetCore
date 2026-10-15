namespace Bing.Application.Aspects;

/// <summary>
/// 工作单元拦截范围。
/// </summary>
public enum BingUnitOfWorkScope
{
    /// <summary>
    /// 每次调用独立执行。
    /// </summary>
    None,

    /// <summary>
    /// 仅抑制直接嵌套调用。
    /// </summary>
    Nested,

    /// <summary>
    /// 同一调用链只保留最外层执行。
    /// </summary>
    Aspect
}
