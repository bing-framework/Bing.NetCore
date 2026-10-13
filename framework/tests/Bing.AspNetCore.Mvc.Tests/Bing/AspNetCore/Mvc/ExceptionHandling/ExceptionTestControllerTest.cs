using System.Net;
using System.Net.Http;
using System.Security.Claims;
using Bing.Security.Claims;

namespace Bing.AspNetCore.Mvc.ExceptionHandling;

/// <summary>
/// 验证控制器异常处理的 HTTP 响应。
/// </summary>
public class ExceptionTestControllerTest : BingAspNetCoreTestBase
{
    /// <summary>
    /// 为异常控制器测试提供模拟用户声明。
    /// </summary>
    private FakeUserClaims _fakeUserClaims;

    /// <summary>
    /// 初始化一个 <see cref="ExceptionTestControllerTest"/> 类型的实例。
    /// </summary>
    public ExceptionTestControllerTest(HttpClient client, FakeUserClaims fakeUserClaims)
    {
        _fakeUserClaims = fakeUserClaims;
        InitClient(client);
    }

    /// <summary>
    /// 测试 - 用户友好异常 - void
    /// </summary>
    [Fact]
    public async Task Test_UserFriendlyException_For_Void_Return_Value()
    {
        var result = await GetResponseAsObjectAsync<TestApiResult>("/api/exception-test/UserFriendlyException1");
        result.Message.ShouldNotBeNull();
        result.Message.ShouldBe("This is a sample exception!");
    }

    /// <summary>
    /// 测试 - 用户友好异常 - ActionResult
    /// </summary>
    [Fact]
    public async Task Test_UserFriendlyException_For_ActionResult_Return_Value()
    {
        var result = await GetResponseAsObjectAsync<TestApiResult>("/api/exception-test/UserFriendlyException2");
        result.Message.ShouldNotBeNull();
        result.Message.ShouldBe("This is a sample exception!");
    }

    //[Fact]
    //public async Task Test_Handle_By_Cookie_AuthenticationScheme_For_BingAuthorizationException_For_Void_Return_Value()
    //{
    //    _fakeUserClaims.Claims.AddRange(new[] { new Claim(BingClaimTypes.UserId, Guid.NewGuid().ToString()) });
    //    var result = await GetResponseAsObjectAsync<TestApiResult>("/api/exception-test/BingAuthorizationException",
    //        HttpStatusCode.Redirect);

    //}
}
