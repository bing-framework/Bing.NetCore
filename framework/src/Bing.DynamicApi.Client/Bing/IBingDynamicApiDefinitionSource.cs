using Bing.DynamicApi.Contracts;
using System.Text.Json;
namespace Bing.DynamicApi.Client;

/// <summary>调用级描述访问上下文；可选 Source 在自身生命周期内维护共享下载，不能捕获调用方 Scoped 服务。</summary>
public sealed record BingDynamicApiDefinitionRequest(string RemoteName, string HttpClientName, string ApiVersion,
    string BaseUrl, string TenantId, string Language, JsonSerializerOptions JsonOptions);
public interface IBingDynamicApiDefinitionSource
{
    string RemoteName { get; }
    /// <summary>令牌只取消当前等待者；共享下载由 Source 的预算及宿主生命周期控制。</summary>
    ValueTask<BingDynamicApiDefinition> GetAsync(BingDynamicApiDefinitionRequest request, CancellationToken cancellationToken);
}
