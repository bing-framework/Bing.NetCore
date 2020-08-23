using System;
using Bing.DependencyInjection;

namespace Bing.Applications
{
    /// <summary>
    /// 应用服务
    /// </summary>
    [IgnoreDependency]
    [Obsolete("请使用IApplicationService接口")]
    public interface IService : IScopedDependency
    {
    }
}
