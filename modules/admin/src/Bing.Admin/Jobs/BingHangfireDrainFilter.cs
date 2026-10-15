using System;
using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Server;

namespace Bing.Admin.Jobs;

/// <summary>
/// 在 Hangfire 排空期间阻止新任务创建或进入执行区。
/// </summary>
public sealed class BingHangfireDrainFilter : JobFilterAttribute, IClientFilter, IServerFilter
{
    /// <summary>
    /// 在途任务状态的过滤器上下文键。
    /// </summary>
    private const string InFlightMarker = "Bing.Admin.Hangfire.InFlight";

    /// <summary>
    /// 管理 Hangfire 任务排空状态的运行时对象。
    /// </summary>
    private readonly BingHangfireRuntime _runtime;

    /// <summary>
    /// 初始化一个 <see cref="BingHangfireDrainFilter" /> 类型的实例。
    /// </summary>
    /// <param name="runtime">任务运行状态。</param>
    public BingHangfireDrainFilter(BingHangfireRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    /// <inheritdoc />
    public void OnCreating(CreatingContext filterContext)
    {
        if (_runtime.IsDraining)
            filterContext.Canceled = true;
    }

    /// <inheritdoc />
    public void OnCreated(CreatedContext filterContext)
    {
    }

    /// <inheritdoc />
    public void OnPerforming(PerformingContext filterContext)
    {
        if (_runtime.TryEnter())
        {
            filterContext.Items[InFlightMarker] = true;
            return;
        }

        filterContext.Canceled = true;
    }

    /// <inheritdoc />
    public void OnPerformed(PerformedContext filterContext)
    {
        if (filterContext.Items.TryGetValue(InFlightMarker, out var value) &&
            value is true)
            _runtime.Exit();
    }
}
