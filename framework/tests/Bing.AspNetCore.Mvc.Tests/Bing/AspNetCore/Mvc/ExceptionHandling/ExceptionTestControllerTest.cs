using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bing.Http;
using Shouldly;
using Xunit;

namespace Bing.AspNetCore.Mvc.ExceptionHandling;

public class ExceptionTestControllerTest : BingAspNetCoreTestBase
{
    /// <summary>
    /// Http客户端
    /// </summary>
    private readonly IHttpClient _client;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public ExceptionTestControllerTest(IHttpClient client)
    {
        _client = client;
    }

    [Fact]
    public async Task Test_Should_Return_RemoteServiceErrorResponse_For_UserFriendlyException_For_Void_Return_Value()
    {
        //var result = await GetResponseAsObjectAsync<ApiResult>("/api/ExceptionTest", HttpStatusCode.Forbidden);
        var result = await _client.Get<ApiResult>("/api/ExceptionTest").GetResultAsync();
        //var result = await GetResponseAsObjectAsync<ApiResult>("/api/exception-test/UserFriendlyException1", HttpStatusCode.Forbidden);
        result.Message.ShouldNotBeNull();
        result.Message.ShouldBe("This is a sample exception!");
    }
}
