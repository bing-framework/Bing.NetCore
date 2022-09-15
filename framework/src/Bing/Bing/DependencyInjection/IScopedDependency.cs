using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection
{
    /// <summary>
    /// 实现此接口的类型将注册为 <see cref="ServiceLifetime.Scoped"/> 模式
    /// </summary>
    /// <remarks>生命周期为每次请求创建一个实例</remarks>
    [IgnoreDependency]
    public interface IScopedDependency
    {
    }
}
