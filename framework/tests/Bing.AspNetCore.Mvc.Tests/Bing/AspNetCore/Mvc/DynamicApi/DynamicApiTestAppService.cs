using System.Net.Http;
using Bing.Application.Services;
using Bing.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 定义动态 API 集成测试使用的应用服务契约。
/// </summary>
public interface IDynamicApiTestAppService : IAppService
{
    /// <summary>
    /// 按标识获取测试对象。
    /// </summary>
    /// <param name="id">测试对象的标识。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>包含测试对象信息的异步操作。</returns>
    Task<string> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按对象标识获取测试对象。
    /// </summary>
    /// <param name="id">测试对象的标识值。</param>
    /// <returns>包含测试对象信息的异步操作。</returns>
    [HttpGet("object/{id}")]
    Task<string> GetByIdAsync(object id);

    /// <summary>
    /// 创建测试对象。
    /// </summary>
    /// <param name="input">测试对象的创建数据。</param>
    /// <returns>包含创建结果的异步操作。</returns>
    Task<DynamicApiTestResponse> CreateAsync(DynamicApiTestRequest input);

    /// <summary>
    /// 按查询词查找测试数据。
    /// </summary>
    /// <param name="term">用于查找的查询词。</param>
    /// <returns>包含查询词的异步操作。</returns>
    [HttpGet("lookup")]
    Task<string> LookupAsync([FromQuery(Name = "q")] string term);

    /// <summary>
    /// 获取受授权策略保护的测试数据。
    /// </summary>
    /// <returns>包含受保护数据的异步操作。</returns>
    [Authorize("MyClaimTestPolicy")]
    Task<string> GetProtectedAsync();

    /// <summary>
    /// 获取用于验证依赖注入激活的数据。
    /// </summary>
    /// <returns>包含激活结果的异步操作。</returns>
    Task<string> GetActivationAsync();

    /// <summary>
    /// 修改测试对象的名称。
    /// </summary>
    /// <param name="id">待修改对象的标识。</param>
    /// <param name="name">对象的新名称。</param>
    /// <returns>包含对象标识和新名称的异步操作。</returns>
    [HttpPatch("rename/{id}")]
    Task<string> RenameAsync(Guid id, string name);

    /// <summary>
    /// 定义应从动态 API 中排除的测试操作。
    /// </summary>
    /// <returns>表示隐藏操作的异步结果。</returns>
    [DisableBingDynamicApi]
    Task<string> HiddenAsync();

    /// <summary>
    /// 接收上传文件并读取其文本内容。
    /// </summary>
    /// <param name="file">上传的远程文件内容。</param>
    /// <returns>包含文件文本内容的异步操作。</returns>
    Task<string> UploadAsync(IRemoteStreamContent file);

    /// <summary>
    /// 获取用于下载测试的远程文件内容。
    /// </summary>
    /// <returns>包含可下载文件的异步操作。</returns>
    [HttpGet("download")]
    Task<IRemoteStreamContent> DownloadAsync();
}

/// <summary>
/// 提供动态 API 集成测试所需的应用服务行为。
/// </summary>
public class DynamicApiTestAppService : AppServiceBase, IDynamicApiTestAppService
{
    /// <inheritdoc />
    public Task<string> GetAsync(Guid entityKey, CancellationToken cancellationToken = default) =>
        Task.FromResult($"item:{entityKey:D}:{cancellationToken.CanBeCanceled}");

    /// <inheritdoc />
    public Task<string> GetByIdAsync(object id) => Task.FromResult($"object:{id}");

    /// <inheritdoc />
    public Task<DynamicApiTestResponse> CreateAsync(DynamicApiTestRequest input) =>
        Task.FromResult(new DynamicApiTestResponse { Name = input.Name, Quantity = input.Quantity });

    /// <inheritdoc />
    public Task<string> LookupAsync(string term) => Task.FromResult(term);

    /// <inheritdoc />
    public Task<string> GetProtectedAsync() => Task.FromResult("protected");

    /// <inheritdoc />
    public virtual Task<string> GetActivationAsync() => Task.FromResult("direct-implementation");

    /// <inheritdoc />
    public Task<string> RenameAsync(Guid id, string name) => Task.FromResult($"{id:D}:{name}");

    /// <inheritdoc />
    public Task<string> HiddenAsync() => Task.FromResult("hidden");

    /// <inheritdoc />
    public async Task<string> UploadAsync(IRemoteStreamContent file)
    {
        using var reader = new StreamReader(file.GetStream());
        return await reader.ReadToEndAsync();
    }

    /// <inheritdoc />
    public Task<IRemoteStreamContent> DownloadAsync() => Task.FromResult<IRemoteStreamContent>(
        new RemoteStreamContent(new MemoryStream(Encoding.UTF8.GetBytes("dynamic-download")), "download.txt", "text/plain"));
}

/// <summary>
/// 定义用于验证服务级禁用行为的动态 API 契约。
/// </summary>
public interface IDisabledDynamicApiTestAppService : IAppService
{
    /// <summary>
    /// 获取禁用服务的测试响应。
    /// </summary>
    /// <returns>包含禁用状态文本的异步操作。</returns>
    Task<string> GetAsync();
}

/// <summary>
/// 提供应整体排除于动态 API 之外的测试服务。
/// </summary>
[DisableBingDynamicApi]
public class DisabledDynamicApiTestAppService : AppServiceBase, IDisabledDynamicApiTestAppService
{
    /// <inheritdoc />
    public Task<string> GetAsync() => Task.FromResult("disabled");
}

/// <summary>
/// 表示动态 API 创建请求的数据。
/// </summary>
public class DynamicApiTestRequest
{
    /// <summary>
    /// 获取或设置测试对象名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置测试对象数量。
    /// </summary>
    public int Quantity { get; set; }
}

/// <summary>
/// 表示动态 API 创建操作的响应数据。
/// </summary>
public class DynamicApiTestResponse
{
    /// <summary>
    /// 获取或设置测试对象名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置测试对象数量。
    /// </summary>
    public int Quantity { get; set; }
}

/// <summary>
/// 覆盖激活结果的动态 API 测试代理。
/// </summary>
public class DynamicApiTestAppServiceProxy : DynamicApiTestAppService
{
    /// <inheritdoc />
    public override Task<string> GetActivationAsync() => Task.FromResult("resolved-through-di-proxy");
}
