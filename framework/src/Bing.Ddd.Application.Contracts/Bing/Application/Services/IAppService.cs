using Bing.DependencyInjection;

namespace Bing.Application.Services;

/// <summary>
/// 应用服务
/// </summary>
[IgnoreDependency]
public interface IAppService : IScopedDependency
{
}