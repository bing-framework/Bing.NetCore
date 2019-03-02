using System.Threading.Tasks;
using Bing.AutoMapper;
using Bing.Biz.OAuthLogin.Wechat;
using Bing.Biz.Tests.Intergration.OAuthLogin.Configs;
using Bing.Mapping;
using Bing.Utils.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.Providers
{
    /// <summary>
    /// 微信授权提供程序测试
    /// </summary>
    public class WechatAuthorizationProviderTest
    {
        /// <summary>
        /// 授权提供程序
        /// </summary>
        private IWechatAuthorizationProvider _provider;

        /// <summary>
        /// 控制台输出
        /// </summary>
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// 初始化一个<see cref="WechatAuthorizationProviderTest"/>类型的实例
        /// </summary>
        public WechatAuthorizationProviderTest(ITestOutputHelper output)
        {
            _output = output;
            _provider = new WechatAuthorizationProvider(new TestWechatAuthorizationConfigProvider());
            MapperExtensions.SetMapper(new AutoMapperMapper());
        }

        /// <summary>
        /// 测试生成授权地址 - 默认回调
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GenerateUrlAsync_Default_Callback()
        {
            var request = new WechatAuthorizationRequest();
            request.Init();
            var result = await _provider.GenerateUrlAsync(request);
            _output.WriteLine(result);
            Assert.Equal($"https://open.weixin.qq.com/connect/qrconnect?appid={TestSampleConfig.WechatAppId}&response_type={request.ResponseType}&scope={request.Scope}&state={request.State}&redirect_uri={Web.UrlEncode(TestSampleConfig.WechatCallbackUrl)}#wechat_redirect", result);
        }

        /// <summary>
        /// 测试生成授权地址 - 自定义回调
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GenerateUrlAsync_Custom_Callback()
        {
            var request = new WechatAuthorizationRequest();
            request.Init();
            request.RedirectUri = TestSampleConfig.WechatCallbackUrl;
            var result = await _provider.GenerateUrlAsync(request);
            _output.WriteLine(result);
            Assert.Equal($"https://open.weixin.qq.com/connect/qrconnect?appid={TestSampleConfig.WechatAppId}&response_type={request.ResponseType}&scope={request.Scope}&state={request.State}&redirect_uri={Web.UrlEncode(TestSampleConfig.WechatCallbackUrl)}#wechat_redirect", result);
        }
    }
}
