using System.Net;
using System.Net.Http;

namespace Bing.AspNetCore.CorrelationIdProvider;

/// <summary>
/// 验证 HTTP 请求中的关联标识传播。
/// </summary>
public class CorrelationIdProviderTest : BingAspNetCoreTestBase
{
    /// <summary>
    /// 初始化一个 <see cref="CorrelationIdProviderTest"/> 类型的实例。
    /// </summary>
    public CorrelationIdProviderTest(HttpClient client)
    {
        InitClient(client);
    }

    /// <summary>
    /// 验证缺少或指定关联标识时响应头均包含对应标识。
    /// </summary>
    [Fact]
    public async Task Test()
    {
        // Test BingCorrelationIdMiddleware without X-Correlation-Id header
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/correlation/404"))
        {
            var response = await Client.SendAsync(requestMessage);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            response.Headers.ShouldContain(x => x.Key == "X-Correlation-Id" && x.Value.First() != null);
        }

        var correlationId = Guid.NewGuid().ToString("N");

        // Test BingCorrelationIdMiddleware
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/correlation/404"))
        {
            requestMessage.Headers.Add("X-Correlation-Id", correlationId);
            var response = await Client.SendAsync(requestMessage);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

            response.Headers.ShouldContain(x => x.Key == "X-Correlation-Id" && x.Value.First() == correlationId);
        }

        // Test AspNetCoreCorrelationIdProvider
        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/correlation/get"))
        {
            requestMessage.Headers.Add("X-Correlation-Id", correlationId);
            var response = await Client.SendAsync(requestMessage);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            (await response.Content.ReadAsStringAsync()).ShouldBe(correlationId);
        }
    }
}
