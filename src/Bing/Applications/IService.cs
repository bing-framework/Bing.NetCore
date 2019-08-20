using Bing.Dependency;

namespace Bing.Applications
{
    /// <summary>
    /// 应用服务
    /// </summary>
    [IgnoreDependency]
    public interface IService : IScopeDependency
    {
    }
}
