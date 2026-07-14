namespace Bing.Logging.BackgroundJobs;

/// <summary>
/// 后台任务委托
/// </summary>
/// <param name="serviceProvider">任务作用域服务提供程序</param>
/// <param name="cancellationToken">取消令牌</param>
public delegate Task BackgroundTaskDelegate(IServiceProvider serviceProvider, CancellationToken cancellationToken);