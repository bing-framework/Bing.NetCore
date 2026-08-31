using System.Security.Principal;
using Bing.DependencyInjection;
using Bing.Security.Claims;

namespace Bing.Clients;

/// <summary>
/// 提供当前安全主体对应客户端的信息访问。
/// </summary>
public class CurrentClient : ICurrentClient, ITransientDependency
{
    /// <summary>
    /// 安全主体访问器
    /// </summary>
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    /// <summary>
    /// 初始化一个 <see cref="CurrentClient"/> 实例。
    /// </summary>
    /// <param name="principalAccessor">当前安全主体访问器。</param>
    public CurrentClient(ICurrentPrincipalAccessor principalAccessor) => _principalAccessor = principalAccessor;

    /// <summary>
    /// 获取当前安全主体声明中的客户端标识；未找到时返回 <see langword="null"/>。
    /// </summary>
    public virtual string Id => _principalAccessor.Principal?.FindClientId();

    /// <summary>
    /// 获取当前客户端是否存在有效标识。
    /// </summary>
    /// <returns>客户端标识不为空时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public virtual bool IsAuthenticated => Id != null;
}
