using Bing.Dependency;

namespace Bing.Domains.Services
{
    /// <summary>
    /// 领域服务
    /// </summary>
    [IgnoreDependency]
    public interface IDomainService : IScopedDependency
    {
    }
}
