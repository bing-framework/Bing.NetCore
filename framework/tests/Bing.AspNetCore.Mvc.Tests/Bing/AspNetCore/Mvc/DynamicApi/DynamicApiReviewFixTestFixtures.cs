using Bing.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 定义用于验证接口代理激活的动态应用服务契约。
/// </summary>
public interface IInterfaceProxyDynamicApiAppService : IAppService
{
    /// <summary>
    /// 获取服务状态文本。
    /// </summary>
    /// <returns>包含服务状态的异步操作。</returns>
    Task<string> GetStatusAsync();
}

/// <summary>
/// 提供接口代理测试所用的应用服务实现。
/// </summary>
public sealed class InterfaceProxyDynamicApiAppService : IInterfaceProxyDynamicApiAppService
{
    /// <inheritdoc />
    public Task<string> GetStatusAsync() => Task.FromResult("direct-service");
}

/// <summary>
/// 提供仅通过服务接口注册的测试代理。
/// </summary>
public sealed class InterfaceOnlyDynamicApiProxy : IInterfaceProxyDynamicApiAppService
{
    /// <inheritdoc />
    public Task<string> GetStatusAsync() => Task.FromResult("interface-only-proxy");
}

/// <summary>
/// 定义显式 MVC 控制器实现的应用服务契约。
/// </summary>
public interface IExplicitDynamicApiControllerService : IAppService
{
    /// <summary>
    /// 获取显式控制器路由对应的文本。
    /// </summary>
    /// <returns>包含路由响应文本的异步操作。</returns>
    Task<string> GetLegacyAsync();
}

/// <summary>
/// 提供用于验证显式控制器路由兼容性的测试控制器。
/// </summary>
[ApiController]
[Route("api/explicit-app-service")]
public sealed class ExplicitDynamicApiController : ControllerBase, IExplicitDynamicApiControllerService
{
    /// <inheritdoc />
    [HttpGet("legacy")]
    public Task<string> GetLegacyAsync() => Task.FromResult("explicit-controller-route");
}

/// <summary>
/// 定义具有重复查询参数名称的测试契约。
/// </summary>
public interface IDuplicateQueryBindingAppService : IAppService
{
    /// <summary>
    /// 获取包含两个同名查询参数的测试结果。
    /// </summary>
    /// <param name="first">第一个查询值。</param>
    /// <param name="second">第二个查询值。</param>
    /// <returns>组合两个查询值的异步操作。</returns>
    [HttpGet("duplicate")]
    Task<string> GetDuplicateAsync([FromQuery(Name = "q")] string first, [FromQuery(Name = "q")] string second);
}

/// <summary>
/// 实现重复查询参数名称测试契约。
/// </summary>
public sealed class DuplicateQueryBindingAppService : IDuplicateQueryBindingAppService
{
    /// <inheritdoc />
    public Task<string> GetDuplicateAsync(string first, string second) => Task.FromResult($"{first}:{second}");
}

/// <summary>
/// 定义具有重复表单参数名称的测试契约。
/// </summary>
public interface IDuplicateFormBindingAppService : IAppService
{
    /// <summary>
    /// 获取包含两个同名表单参数的测试结果。
    /// </summary>
    /// <param name="first">第一个表单值。</param>
    /// <param name="second">第二个表单值。</param>
    /// <returns>组合两个表单值的异步操作。</returns>
    [HttpPost("duplicate")]
    Task<string> PostDuplicateAsync([FromForm(Name = "tag")] string first, [FromForm(Name = "tag")] string second);
}

/// <summary>
/// 实现重复表单参数名称测试契约。
/// </summary>
public sealed class DuplicateFormBindingAppService : IDuplicateFormBindingAppService
{
    /// <inheritdoc />
    public Task<string> PostDuplicateAsync(string first, string second) => Task.FromResult($"{first}:{second}");
}

/// <summary>
/// 定义从不同来源读取同名参数的测试契约。
/// </summary>
public interface IDistinctSourceBindingAppService : IAppService
{
    /// <summary>
    /// 获取查询参数和请求头中的同名值。
    /// </summary>
    /// <param name="query">从查询字符串读取的值。</param>
    /// <param name="header">从请求头读取的值。</param>
    /// <returns>组合查询值与请求头值的异步操作。</returns>
    [HttpGet("distinct")]
    Task<string> GetDistinctAsync([FromQuery(Name = "value")] string query, [FromHeader(Name = "value")] string header);
}

/// <summary>
/// 实现不同参数来源使用相同名称的测试契约。
/// </summary>
public sealed class DistinctSourceBindingAppService : IDistinctSourceBindingAppService
{
    /// <inheritdoc />
    public Task<string> GetDistinctAsync(string query, string header) => Task.FromResult($"{query}:{header}");
}
