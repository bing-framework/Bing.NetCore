using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Service.Events;
using Bing.AspNetCore.Mvc;
using Bing.Events.Ordering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Admin.Apis.Events;

/// <summary>
/// 提供受保护的有序事件 Prometheus 指标。
/// </summary>
/// <remarks>
/// 控制器始终参与 MVC 发现，但只有宿主显式启用指标诊断并注册授权器时才返回内容。
/// 输出只包含低基数聚合指标，不包含事件正文、事件标识或实体键。
/// </remarks>
[Authorize]
public sealed class OrderedEventMetricsController : ApiControllerBase
{
    /// <summary>
    /// 提供有序事件指标快照的读取器。
    /// </summary>
    private readonly IOrderedEventMetricsSnapshotProvider _provider;

    /// <summary>
    /// 校验指标读取权限的授权器。
    /// </summary>
    private readonly AdminOrderedEventMetricsAuthorizer _authorizer;

    /// <summary>
    /// 初始化一个 <see cref="OrderedEventMetricsController" /> 类型的实例。
    /// </summary>
    /// <param name="provider">有序事件指标快照提供程序；未启用诊断时为空。</param>
    /// <param name="authorizer">指标读取授权器；未启用诊断时为空。</param>
    public OrderedEventMetricsController(IOrderedEventMetricsSnapshotProvider provider = null,
        AdminOrderedEventMetricsAuthorizer authorizer = null)
    {
        _provider = provider;
        _authorizer = authorizer;
    }

    /// <summary>
    /// 获取有序事件 Prometheus 指标。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>Prometheus 文本或诊断功能未启用时的 404。</returns>
    [HttpGet("~/internal/metrics/ordered-events")]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        if (_provider == null || _authorizer == null)
            return NotFound();

        await _authorizer.AuthorizeAsync(cancellationToken).ConfigureAwait(false);
        var content = BingOrderedEventMetricsPrometheusExporter.Export(_provider);
        return Content(content, "text/plain; version=0.0.4", Encoding.UTF8);
    }
}
