using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Bing.AspNetCore.Mvc;
using Bing.Data.Events.Ordering;
using Bing.Events.Ordering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Admin.Apis.Events;

/// <summary>
/// 提供受授权的有序事件状态查询和人工重试入口。
/// </summary>
/// <remarks>
/// 控制器始终参与 MVC 发现，但只有宿主注册恢复服务时才启用实际操作；关闭恢复配置时返回 404。
/// 租户、消费者和业务流权限由 <see cref="IOrderedEventRecoveryService"/> 的授权器统一判断。
/// </remarks>
[Authorize]
public sealed class OrderedEventRecoveryController : ApiControllerBase
{
    /// <summary>
    /// 查询状态并执行人工恢复的应用服务。
    /// </summary>
    private readonly IOrderedEventRecoveryService _service;

    /// <summary>
    /// 初始化一个 <see cref="OrderedEventRecoveryController" /> 类型的实例。
    /// </summary>
    /// <param name="service">有序事件恢复服务；未启用恢复功能时为空。</param>
    public OrderedEventRecoveryController(IOrderedEventRecoveryService service = null)
    {
        _service = service;
    }

    /// <summary>
    /// 查询指定业务流的有序事件状态。
    /// </summary>
    /// <param name="request">业务流查询条件。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>业务流状态或恢复功能未启用时的 404。</returns>
    [HttpGet("~/api/ordered-events/recovery/state")]
    public async Task<IActionResult> GetStateAsync([FromQuery] OrderedEventRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        if (_service == null)
            return NotFound();
        if (request == null)
            return BadRequest();
        var state = await _service.GetStateAsync(request.Consumer, request.ToStream(), cancellationToken)
            .ConfigureAwait(false);
        return Success(state);
    }

    /// <summary>
    /// 重试指定业务流的失败前序事件。
    /// </summary>
    /// <param name="request">业务流恢复条件。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>操作成功或恢复功能未启用时的 404。</returns>
    [HttpPost("~/api/ordered-events/recovery/retry")]
    public async Task<IActionResult> RetryAsync([FromBody] OrderedEventRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        if (_service == null)
            return NotFound();
        if (request == null)
            return BadRequest();
        await _service.RetryAsync(request.Consumer, request.ToStream(), cancellationToken)
            .ConfigureAwait(false);
        return Success();
    }
}

/// <summary>
/// 表示有序事件恢复接口使用的业务流请求。
/// </summary>
public sealed class OrderedEventRecoveryRequest
{
    /// <summary>
    /// 获取逻辑消费者名称。
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Consumer { get; set; }

    /// <summary>
    /// 获取租户标识；全局业务流为空。
    /// </summary>
    [StringLength(200)]
    public string TenantId { get; set; }

    /// <summary>
    /// 获取稳定的业务流名称。
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Stream { get; set; }

    /// <summary>
    /// 获取实体业务键。
    /// </summary>
    [Required]
    [StringLength(200)]
    public string EntityKey { get; set; }

    /// <summary>
    /// 将请求转换为有序事件业务流。
    /// </summary>
    /// <returns>已校验长度和必填字段的业务流。</returns>
    public OrderedEventStream ToStream() => new(TenantId ?? string.Empty, Stream, EntityKey);
}
