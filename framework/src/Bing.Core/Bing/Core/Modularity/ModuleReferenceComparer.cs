using System.Runtime.CompilerServices;

namespace Bing.Core.Modularity;

/// <summary>
/// 按模块实例身份比较模块引用。
/// </summary>
/// <remarks>
/// 资源所有权判断使用引用相等，不调用模块自定义的值相等实现。
/// </remarks>
internal sealed class ModuleReferenceComparer : IEqualityComparer<BingModule>
{
    /// <summary>
    /// 获取模块引用比较器的共享实例。
    /// </summary>
    internal static readonly ModuleReferenceComparer Instance = new();

    /// <summary>
    /// 初始化模块引用比较器。
    /// </summary>
    private ModuleReferenceComparer() { }

    /// <inheritdoc />
    public bool Equals(BingModule x, BingModule y) => ReferenceEquals(x, y);

    /// <inheritdoc />
    public int GetHashCode(BingModule obj) => RuntimeHelpers.GetHashCode(obj);
}
