using Bing.DependencyInjection;

namespace Bing.Domain.Services
{
    /// <summary>
    /// 领域服务
    /// </summary>
    [IgnoreDependency]
    public interface IDomainService : IScopedDependency
    {
    }
}
