using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 基于<see cref="ServiceLifetime.Scoped"/>生命周期的数据字典。可用于在Scoped环境中传递数据
/// </summary>
[Dependency(ServiceLifetime.Scoped, AddSelf = true)]
public class ScopedDictionary : ConcurrentDictionary<string, object>, IDisposable
{
    /// <summary>
    /// 对于当前功能有效的角色集合，用于数据权限判断
    /// </summary>
    public string[] DataAuthValidRoleNames { get; set; } = new string[0];

    /// <summary>
    /// 当前用户
    /// </summary>
    public ClaimsIdentity Identity { get; set; }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Identity = null;
        this.Clear();
    }
}
