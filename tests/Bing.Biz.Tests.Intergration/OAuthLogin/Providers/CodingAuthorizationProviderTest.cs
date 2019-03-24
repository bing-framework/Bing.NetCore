using System.Threading.Tasks;
using Bing.AutoMapper;
using Bing.Biz.OAuthLogin.Coding;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.Tests.Intergration.OAuthLogin.Configs;
using Bing.Mapping;
using Bing.Utils.Helpers;
using Bing.Utils.Json;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.Providers
{
    /// <summary>
    /// Coding.NET授权提供程序测试
    /// </summary>
    public class CodingAuthorizationProviderTest
    {
        /// <summary>
        /// 授权提供程序
        /// </summary>
        private ICodingAuthorizationProvider _provider;

        /// <summary>
        /// 控制台输出
        /// </summary>
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// 初始化一个<see cref="CodingAuthorizationProviderTest"/>类型的实例
        /// </summary>
        public CodingAuthorizationProviderTest(ITestOutputHelper output)
        {
            _output = output;
            _provider = new CodingAuthorizationProvider(new TestCodingAuthorizationConfigProvider());
            MapperExtensions.SetMapper(new AutoMapperMapper());
        }

        /// <summary>
        /// 测试生成授权地址 - 默认回调
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GenerateUrlAsync_Default_Callback()
        {
            var request = new CodingAuthorizationRequest();
            request.Init();
            request.Scope = "user";
            var result = await _provider.GenerateUrlAsync(request);
            _output.WriteLine(result);
            Assert.Equal($"https://coding.net/oauth_authorize.html?client_id={TestSampleConfig.CodingAppId}&redirect_uri={Web.UrlEncode(TestSampleConfig.CodingCallbackUrl)}&response_type=code&scope=user", result);
        }

        /// <summary>
        /// 测试生成授权地址 - 自定义回调
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GenerateUrlAsync_Custom_Callback()
        {
            var request = new CodingAuthorizationRequest();
            request.Init();
            request.Scope = "user";
            request.RedirectUri = TestSampleConfig.CodingCallbackUrl;
            var result = await _provider.GenerateUrlAsync(request);
            _output.WriteLine(result);
            Assert.Equal($"https://coding.net/oauth_authorize.html?client_id={TestSampleConfig.CodingAppId}&redirect_uri={Web.UrlEncode(TestSampleConfig.CodingCallbackUrl)}&response_type=code&scope=user", result);
        }

        ///// <summary>
        ///// 测试获取访问令牌
        ///// </summary>
        ///// <returns></returns>
        //[Fact]
        //public async Task Test_GetTokenAsync()
        //{
        //    var param = new AccessTokenParam();
        //    param.Code = "";
        //    param.RedirectUri = TestSampleConfig.CodingCallbackUrl;
        //    var result = await _provider.GetTokenAsync(param);
        //    _output.WriteLine(result.ToJson());
        //    Assert.NotNull(result);
        //}

        ///// <summary>
        ///// 测试获取用户信息
        ///// </summary>
        ///// <returns></returns>
        //[Fact]
        //public async Task Test_GetUserInfoAsync()
        //{
        //    var result = await _provider.GetUserInfoAsync(new CodingAuthorizationUserRequest()
        //    {
        //        AccessToken = "",
        //    });
        //    _output.WriteLine(result.ToJson());
        //    Assert.NotNull(result);
        //}

    }
}
