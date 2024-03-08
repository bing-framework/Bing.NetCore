using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bing.Utils.Json;
using Microsoft.Net.Http.Headers;
using Shouldly;

namespace Bing.AspNetCore;

/// <summary>
/// AspNetCore测试基类
/// </summary>
public abstract class BingAspNetCoreTestBase
{
    /// <summary>
    /// Http客户端
    /// </summary>
    protected HttpClient Client { get; private set; }

    /// <summary>
    /// 初始化客户端
    /// </summary>
    protected void InitClient(HttpClient client) => Client = client;

    /// <summary>
    /// 从给定URL获取响应，并将响应内容反序列化为指定的对象类型。
    /// </summary>
    /// <typeparam name="T">期望反序列化的对象类型</typeparam>
    /// <param name="url">请求地址</param>
    /// <param name="expectedStatusCode">期望的HTTP状态码，默认为 <see cref="HttpStatusCode.OK"/>。</param>
    protected virtual async Task<T> GetResponseAsObjectAsync<T>(string url, HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
    {
        var strResponse = await GetResponseAsStringAsync(url, expectedStatusCode);
        return JsonHelper.ToObject<T>(strResponse);
    }

    /// <summary>
    /// 从给定URL发送GET请求并获取响应，并以字符串形式返回响应内容。
    /// </summary>
    /// <param name="url">请求地址</param>
    /// <param name="expectedStatusCode">期望的HTTP状态码，默认为 <see cref="HttpStatusCode.OK"/>。</param>
    protected virtual async Task<string> GetResponseAsStringAsync(string url, HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
    {
        using var response = await GetResponseAsync(url, expectedStatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// 从给定URL发送GET请求并获取HttpResponseMessage对象。
    /// </summary>
    /// <param name="url">请求地址</param>
    /// <param name="expectedStatusCode">期望的HTTP状态码，默认为 <see cref="HttpStatusCode.OK"/>。</param>
    /// <param name="xmlHttpRequest">指定请求是否应被视为XMLHttpRequest，默认为false</param>
    protected virtual async Task<HttpResponseMessage> GetResponseAsync(string url, HttpStatusCode expectedStatusCode = HttpStatusCode.OK, bool xmlHttpRequest = false)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        requestMessage.Headers.Add("Accept-Language", CultureInfo.CurrentUICulture.Name);
        if (xmlHttpRequest)
            requestMessage.Headers.Add(HeaderNames.XRequestedWith, "XMLHttpRequest");
        var response = await Client.SendAsync(requestMessage);
        response.StatusCode.ShouldBe(expectedStatusCode);
        return response;
    }
}
