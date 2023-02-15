using Bing.Aspects;
using Bing.DependencyInjection;
using Bing.Linq;
using Bing.Logging;
using Bing.Users;

namespace Bing.Application.Services;

/// <summary>
/// 应用服务基类
/// </summary>
public abstract class AppServiceBase : IAppService
{
    /// <summary>
    /// Lazy延迟加载服务提供程序
    /// </summary>
    [Autowired]
    public virtual ILazyServiceProvider LazyServiceProvider { get; set; }

    /// <summary>
    /// 当前用户
    /// </summary>
    protected ICurrentUser CurrentUser => LazyServiceProvider.LazyGetRequiredService<ICurrentUser>();

    /// <summary>
    /// 异步查询执行器
    /// </summary>
    protected IAsyncQueryableExecuter AsyncExecuter => LazyServiceProvider.LazyGetRequiredService<IAsyncQueryableExecuter>();

    /// <summary>
    /// 日志工厂
    /// </summary>
    protected ILogFactory LogFactory => LazyServiceProvider.LazyGetRequiredService<ILogFactory>();

    /// <summary>
    /// 日志操作
    /// </summary>
    protected ILog Log => LazyServiceProvider.LazyGetService<ILog>(provider => LogFactory?.CreateLog(GetType().FullName) ?? NullLog.Instance);
}
