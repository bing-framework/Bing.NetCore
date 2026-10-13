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
    /// 初始化一个 <see cref="CurrentClient"/> 类型的实例。
    /// </summary>
    /// <param name="principalAccessor">当前安全主体访问器。</param>
    public CurrentClient(ICurrentPrincipalAccessor principalAccessor) => _principalAccessor = principalAccessor;

    /// <inheritdoc />
    /// <remarks>
    /// 获取当前安全主体声明中的客户端标识。
    ///
    /// 未找到时返回 <see langword="null"/>。
    /// </remarks>
    public virtual string Id => _principalAccessor.Principal?.FindClientId();

    /// <inheritdoc />
    public virtual bool IsAuthenticated => Id != null;
}
