using System.Threading.Tasks;
using Bing.AutoMapper;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.QQ;
using Bing.Biz.Tests.Intergration.OAuthLogin.QQ.Configs;
using Bing.Mapping;
using Bing.Utils.Helpers;
using Bing.Utils.Json;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.QQ
{
    /// <summary>
    /// QQ授权提供程序测试
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class QQAuthorizationProviderTest
    {
        /// <summary>
        /// 授权提供程序
        /// </summary>
        private IQQAuthorizationProvider _provider;

        /// <summary>
        /// 控制台输出
        /// </summary>
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// 初始化一个<see cref="QQAuthorizationProviderTest"/>类型的实例
        /// </summary>
        public QQAuthorizationProviderTest(ITestOutputHelper output)
        {
            _output = output;
            _provider = new QQAuthorizationProvider(new TestQQAuthorizationConfigProvider());
            MapperExtensions.SetMapper(new AutoMapperMapper());
        }

        /// <summary>
        /// 测试生成授权地址 - 默认回调
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GenerateUrlAsync_Default_Callback()
        {
            var request = new QQAuthorizationRequest();
            request.Init();
            var result = await _provider.GenerateUrlAsync(request);
            _output.WriteLine(result);
            Assert.Equal($"https://graph.qq.com/oauth2.0/authorize?client_id={TestSampleConfig.QQAppId}&response_type={request.ResponseType}&state={request.State}&redirect_uri={Web.UrlEncode(TestSampleConfig.QQCallbackUrl)}", result);
        }

        /// <summary>
        /// 测试生成授权地址 - 自定义回调
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GenerateUrlAsync_Custom_Callback()
        {
            var request = new QQAuthorizationRequest();
            request.Init();
            request.RedirectUri = TestSampleConfig.QQCallbackUrl;
            var result = await _provider.GenerateUrlAsync(request);
            _output.WriteLine(result);
            Assert.Equal($"https://graph.qq.com/oauth2.0/authorize?client_id={TestSampleConfig.QQAppId}&response_type={request.ResponseType}&state={request.State}&redirect_uri={Web.UrlEncode(TestSampleConfig.QQCallbackUrl)}", result);
        }

        /// <summary>
        /// 测试获取访问令牌
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GetTokenAsync()
        {
            var param = new AccessTokenParam();
            param.Code = "AF38F162625993BFCCBE1A079525BAAE";
            param.RedirectUri = TestSampleConfig.QQCallbackUrl;
            var result = await _provider.GetTokenAsync(param);
            _output.WriteLine(result.ToJson());
            Assert.NotNull(result);
        }

        /// <summary>
        /// 测试刷新令牌
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_RefreshTokenAsync()
        {
            var refreshToken = "303F6BF38E7B03C4A3CD11CA1FCA6635";
            var result = await _provider.RefreshTokenAsync(refreshToken);
            _output.WriteLine(result.ToJson());
            Assert.NotNull(result);
        }
    }
}
