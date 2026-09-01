using Bing.DependencyInjection;

namespace Bing.Application.Services;

/// <summary>
/// 定义应用服务契约。
/// </summary>
[IgnoreDependency]
public interface IAppService : IScopedDependency
{
}