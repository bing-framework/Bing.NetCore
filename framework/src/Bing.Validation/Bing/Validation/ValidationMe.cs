namespace Bing.Validation;

/// <summary>
/// 验证组件静态入口
/// </summary>
public static class ValidationMe
{
    /// <summary>
    /// 默认验证回调处理器
    /// </summary>
    private static IValidationCallbackHandler DefaultCallbackHandler { get; set; }

    /// <summary>
    /// 初始化一个<see cref="ValidationMe"/>静态构造函数
    /// </summary>
    static ValidationMe()
    {
        DefaultCallbackHandler = new ThrowHandler();
    }

    /// <summary>
    /// 注册验证回调处理器
    /// </summary>
    /// <param name="handler">验证回调处理器</param>
    public static void RegisterCallbackHandler(IValidationCallbackHandler handler) => DefaultCallbackHandler = handler;

    /// <summary>
    /// 公开验证回调处理器
    /// </summary>
    internal static IValidationCallbackHandler ExposeCallbackHandler() => DefaultCallbackHandler;
}
