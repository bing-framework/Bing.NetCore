using System.Threading.Tasks;
using Bing.AutoMapper;
using Bing.Biz.OAuthLogin.Github;
using Bing.Biz.Tests.Intergration.OAuthLogin.Configs;
using Bing.Mapping;
using Bing.Utils.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.Providers
{
    /// <summary>
    /// Github授权提供程序测试
    /// </summary>
    public class GithubAuthorizationProviderTest
    {
        /// <summary>
        /// 授权提供程序
        /// </summary>
        private IGithubAuthorizationProvider _provider;

        /// <summary>
        /// 控制台输出
        /// </summary>
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// 初始化一个<see cref="GithubAuthorizationProviderTest"/>类型的实例
        /// </summary>
        public GithubAuthorizationProviderTest(ITestOutputHelper output)
        {
            _output = output;
            _provider = new GithubAuthorizationProvider(new TestGithubAuthorizationConfigProvider());
            MapperExtensions.SetMapper(new AutoMapperMapper());
        }

        /// <summary>
        /// 测试生成授权地址 - 默认回调
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GenerateUrlAsync_Default_Callback()
        {
            var request = new GithubAuthorizationRequest();
            request.Init();
            request.Scope = "user:email";
            var result = await _provider.GenerateUrlAsync(request);
            _output.WriteLine(result);
            Assert.Equal($"https://github.com/login/oauth/authorize?client_id={TestSampleConfig.GithubAppId}&scope={request.Scope}&state={request.State}&redirect_uri={Web.UrlEncode(TestSampleConfig.GithubCallbackUrl)}", result);
        }

        /// <summary>
        /// 测试生成授权地址 - 自定义回调
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GenerateUrlAsync_Custom_Callback()
        {
            var request = new GithubAuthorizationRequest();
            request.Init();
            request.Scope = "user:email";
            request.RedirectUri = TestSampleConfig.GithubCallbackUrl;
            var result = await _provider.GenerateUrlAsync(request);
            _output.WriteLine(result);
            Assert.Equal($"https://github.com/login/oauth/authorize?client_id={TestSampleConfig.GithubAppId}&scope={request.Scope}&state={request.State}&redirect_uri={Web.UrlEncode(TestSampleConfig.GithubCallbackUrl)}", result);
        }
    }
}
