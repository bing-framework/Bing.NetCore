using System.Threading.Tasks;
using Bing.AutoMapper;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Wechat;
using Bing.Biz.Tests.Intergration.OAuthLogin.Configs;
using Bing.Mapping;
using Bing.Utils.Helpers;
using Bing.Utils.Json;
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
        private readonly IWechatAuthorizationProvider _provider;

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
            Assert.Equal($"https://open.weixin.qq.com/connect/qrconnect?appid={TestSampleConfig.WechatAppId}&redirect_uri={Web.UrlEncode(TestSampleConfig.WechatCallbackUrl)}&response_type={request.ResponseType}&scope={request.Scope}&state={request.State}#wechat_redirect", result);
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
            Assert.Equal($"https://open.weixin.qq.com/connect/qrconnect?appid={TestSampleConfig.WechatAppId}&redirect_uri={Web.UrlEncode(TestSampleConfig.WechatCallbackUrl)}&response_type={request.ResponseType}&scope={request.Scope}&state={request.State}#wechat_redirect", result);
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
        //    param.RedirectUri = TestSampleConfig.WechatCallbackUrl;
        //    var result = await _provider.GetTokenAsync(param);
        //    _output.WriteLine(result.ToJson());
        //    Assert.NotNull(result);
        //}

        ///// <summary>
        ///// 测试刷新令牌
        ///// </summary>
        ///// <returns></returns>
        //[Fact]
        //public async Task Test_RefreshTokenAsync()
        //{
        //    var refreshToken = "";
        //    var result = await _provider.RefreshTokenAsync(refreshToken);
        //    _output.WriteLine(result.ToJson());
        //    Assert.NotNull(result);
        //}

        ///// <summary>
        ///// 测试获取用户信息
        ///// </summary>
        ///// <returns></returns>
        //[Fact]
        //public async Task GetUserInfoAsync()
        //{
        //    var result = await _provider.GetUserInfoAsync(new WechatAuthorizationUserRequest()
        //    {
        //        AccessToken = "",
        //        OpenId = ""
        //    });
        //    _output.WriteLine(result.ToJson());
        //    Assert.NotNull(result);
        //}
    }
}
