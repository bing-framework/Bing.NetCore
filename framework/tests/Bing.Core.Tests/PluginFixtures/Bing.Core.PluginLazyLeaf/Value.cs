namespace Bing.Core.PluginLazyLeaf;

/// <summary>
/// 提供私有依赖链末端的返回值。
/// </summary>
public static class Value
{
    /// <summary>
    /// 读取测试值。
    /// </summary>
    /// <returns>测试值。</returns>
    public static string Read() => "private";
}
