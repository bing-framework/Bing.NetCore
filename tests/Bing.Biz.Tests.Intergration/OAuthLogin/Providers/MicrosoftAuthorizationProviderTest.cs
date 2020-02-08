using System.Threading.Tasks;
using Bing.AutoMapper;
using Bing.Biz.OAuthLogin.Microsoft;
using Bing.Biz.Tests.Intergration.OAuthLogin.Configs;
using Bing.Helpers;
using Bing.Mapping;
using Bing.Utils.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.Providers
{
    /// <summary>
    /// Microsoft 授权提供程序测试
    /// </summary>
    public class MicrosoftAuthorizationProviderTest
    {
        /// <summary>
        /// 授权提供程序
        /// </summary>
        private IMicrosoftAuthorizationProvider _provider;

        /// <summary>
        /// 控制台输出
        /// </summary>
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// 初始化一个<see cref="MicrosoftAuthorizationProviderTest"/>类型的实例
        /// </summary>
        public MicrosoftAuthorizationProviderTest(ITestOutputHelper output)
        {
            _output = output;
            _provider = new MicrosoftAuthorizationProvider(new TestMicrosoftAuthorizationConfigProvider());
            MapperExtensions.SetMapper(new AutoMapperMapper());
        }

        /// <summary>
        /// 测试生成授权地址 - 默认回调
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GenerateUrlAsync_Default_Callback()
        {
            var request = new MicrosoftAuthorizationRequest();
            request.Scope = "admin";
            request.Init();
            var result = await _provider.GenerateUrlAsync(request);
            _output.WriteLine(result);
            Assert.Equal($"https://login.live.com/oauth20_authorize.srf?client_id={TestSampleConfig.MicrosoftAppId}&scope={request.Scope}&response_type={request.ResponseType}&redirect_uri={Web.UrlEncode(TestSampleConfig.MicrosoftCallbackUrl)}", result);
        }

        /// <summary>
        /// 测试生成授权地址 - 自定义回调
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GenerateUrlAsync_Custom_Callback()
        {
            var request = new MicrosoftAuthorizationRequest();
            request.Init();
            request.Scope = "admin";
            request.RedirectUri = TestSampleConfig.MicrosoftCallbackUrl;
            var result = await _provider.GenerateUrlAsync(request);
            _output.WriteLine(result);
            Assert.Equal($"https://login.live.com/oauth20_authorize.srf?client_id={TestSampleConfig.MicrosoftAppId}&scope={request.Scope}&response_type={request.ResponseType}&redirect_uri={Web.UrlEncode(TestSampleConfig.MicrosoftCallbackUrl)}", result);
        }
    }
}
