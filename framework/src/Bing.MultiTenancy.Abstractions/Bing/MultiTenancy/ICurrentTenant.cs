namespace Bing.MultiTenancy;

/// <summary>
/// 提供当前异步执行上下文中的租户信息。
/// </summary>
public interface ICurrentTenant
{
    /// <summary>
    /// 获取当前执行上下文是否已解析到可用租户。
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// 获取当前租户标识；未解析到租户时返回 <c>null</c>。
    /// </summary>
    string? Id { get; }

    /// <summary>
    /// 获取当前租户名称；未解析到租户或名称不可用时返回 <c>null</c>。
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// 在当前异步执行上下文中临时切换租户。
    /// </summary>
    /// <param name="id">租户标识</param>
    /// <param name="name">要切换到的租户名称；可以为 <c>null</c>。</param>
    /// <returns>用于恢复切换前租户上下文的作用域句柄。</returns>
    IDisposable Change(string id, string? name = null);
}
