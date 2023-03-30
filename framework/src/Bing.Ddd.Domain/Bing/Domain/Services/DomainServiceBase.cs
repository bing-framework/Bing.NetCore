using Bing.Aspects;
using Bing.DependencyInjection;
using Bing.Logging;

namespace Bing.Domain.Services;

/// <summary>
/// 领域服务抽象基类
/// </summary>
public abstract class DomainServiceBase : IDomainService
{
    /// <summary>
    /// 初始化一个<see cref="DomainServiceBase"/>类型的实例
    /// </summary>
    protected DomainServiceBase() { }

    /// <summary>
    /// Lazy延迟加载服务提供程序
    /// </summary>
    [Autowired]
    public virtual ILazyServiceProvider LazyServiceProvider { get; set; }

    /// <summary>
    /// 日志工厂
    /// </summary>
    protected ILogFactory LogFactory => LazyServiceProvider.LazyGetRequiredService<ILogFactory>();

    /// <summary>
    /// 日志
    /// </summary>
    protected ILog Log => LazyServiceProvider.LazyGetService<ILog>(provider => LogFactory?.CreateLog(GetType().FullName) ?? NullLog.Instance);
}
