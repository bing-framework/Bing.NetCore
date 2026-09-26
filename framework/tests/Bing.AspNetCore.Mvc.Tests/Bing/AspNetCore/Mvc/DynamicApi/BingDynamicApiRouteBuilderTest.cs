using System.Reflection;
using Bing.AspNetCore.Mvc.DynamicApi;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 动态 API 路由约定测试。
/// </summary>
public class BingDynamicApiRouteBuilderTest
{
    /// <summary>
    /// 验证服务名称会移除契约后缀并转换为 kebab-case。
    /// </summary>
    /// <param name="input">待转换的服务契约名称。</param>
    /// <param name="expected">预期的服务路由名称。</param>
    [Theory]
    [InlineData("IOrderAppService", "order")]
    [InlineData("IUserProfileService", "user-profile")]
    public void NormalizeServiceName_RemovesServiceContractSuffix(string input, string expected)
    {
        BingDynamicApiRouteBuilder.NormalizeServiceName(input).ShouldBe(expected);
    }

    /// <summary>
    /// 验证方法命名约定生成的 HTTP 动词和路由片段。
    /// </summary>
    /// <param name="methodName">待检查的方法名称。</param>
    /// <param name="expectedVerb">预期的 HTTP 动词。</param>
    /// <param name="expectedPath">预期的方法路由片段。</param>
    [Theory]
    [InlineData(nameof(RouteSamples.GetAsync), "GET", "{id}")]
    [InlineData(nameof(RouteSamples.GetByIdAsync), "GET", "{orderId}")]
    [InlineData(nameof(RouteSamples.GetListAsync), "GET", "")]
    [InlineData(nameof(RouteSamples.CreateAsync), "POST", "")]
    [InlineData(nameof(RouteSamples.UpdateAsync), "PUT", "{id}")]
    [InlineData(nameof(RouteSamples.DeleteAsync), "DELETE", "{id}")]
    [InlineData(nameof(RouteSamples.PatchAsync), "PATCH", "{id}")]
    [InlineData(nameof(RouteSamples.RunAsync), "POST", "run")]
    public void RouteConvention_UsesExpectedVerbAndPath(string methodName, string expectedVerb, string expectedPath)
    {
        var method = typeof(RouteSamples).GetMethod(methodName)!;
        BingDynamicApiRouteBuilder.GetHttpMethod(method).ShouldBe(expectedVerb);
        BingDynamicApiRouteBuilder.BuildMethodRoute(method).ShouldBe(expectedPath);
    }

    /// <summary>
    /// 提供用于验证路由约定的示例方法。
    /// </summary>
    private class RouteSamples
    {
        /// <summary>
        /// 提供按标识查询的示例方法。
        /// </summary>
        /// <param name="id">查询对象的标识。</param>
        /// <returns>包含标识文本的异步操作。</returns>
        public Task<string> GetAsync(Guid id) => Task.FromResult(id.ToString());

        /// <summary>
        /// 提供按对象标识查询的示例方法。
        /// </summary>
        /// <param name="orderId">订单标识。</param>
        /// <returns>包含订单标识文本的异步操作。</returns>
        public Task<string> GetByIdAsync(Guid orderId) => Task.FromResult(orderId.ToString());

        /// <summary>
        /// 提供列表查询的示例方法。
        /// </summary>
        /// <returns>包含空文本的异步操作。</returns>
        public Task<string> GetListAsync() => Task.FromResult(string.Empty);

        /// <summary>
        /// 提供创建操作的示例方法。
        /// </summary>
        /// <param name="input">创建所需的输入对象。</param>
        /// <returns>包含空标识的异步操作。</returns>
        public Task<Guid> CreateAsync(object input) => Task.FromResult(Guid.Empty);

        /// <summary>
        /// 提供更新操作的示例方法。
        /// </summary>
        /// <param name="id">待更新对象的标识。</param>
        /// <param name="input">更新所需的输入对象。</param>
        public Task UpdateAsync(Guid id, object input) => Task.CompletedTask;

        /// <summary>
        /// 提供删除操作的示例方法。
        /// </summary>
        /// <param name="id">待删除对象的标识。</param>
        public Task DeleteAsync(Guid id) => Task.CompletedTask;

        /// <summary>
        /// 提供局部更新操作的示例方法。
        /// </summary>
        /// <param name="id">待更新对象的标识。</param>
        /// <param name="input">局部更新所需的输入对象。</param>
        public Task PatchAsync(Guid id, object input) => Task.CompletedTask;

        /// <summary>
        /// 提供未匹配其他动词前缀的操作示例。
        /// </summary>
        /// <returns>表示操作完成的异步操作。</returns>
        public Task RunAsync() => Task.CompletedTask;
    }
}
