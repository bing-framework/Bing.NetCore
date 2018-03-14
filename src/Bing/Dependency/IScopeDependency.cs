using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Dependency
{
    /// <summary>
    /// 实现此接口的类型将注册为<see cref="LifetimeStyle.Scoped"/>模式
    /// </summary>
    public interface IScopeDependency : IDependency
    {
    }
}
