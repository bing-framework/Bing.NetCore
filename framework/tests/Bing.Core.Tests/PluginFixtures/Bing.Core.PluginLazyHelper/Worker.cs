namespace Bing.Core.PluginLazyHelper;

/// <summary>
/// 私有传递依赖的测试调用器。
/// </summary>
public static class Worker
{
    /// <summary>
    /// 读取测试值。
    /// </summary>
    /// <returns>私有传递依赖的测试值。</returns>
    public static string Read() => Bing.Core.PluginLazyLeaf.Value.Read();
}
