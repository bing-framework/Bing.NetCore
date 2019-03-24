using System.Threading.Tasks;
using Bing.AutoMapper;
using Bing.Biz.OAuthLogin.Core;
using Bing.Biz.OAuthLogin.Weibo;
using Bing.Biz.Tests.Intergration.OAuthLogin.Configs;
using Bing.Mapping;
using Bing.Utils.Helpers;
using Bing.Utils.Json;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Biz.Tests.Intergration.OAuthLogin.Providers
{
    /// <summary>
    /// 微博授权提供程序测试
    /// </summary>
    public class WeiboAuthorizationProviderTest
    {
        /// <summary>
        /// 授权提供程序
        /// </summary>
        private IWeiboAuthorizationProvider _provider;

        /// <summary>
        /// 控制台输出
        /// </summary>
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// 初始化一个<see cref="WeiboAuthorizationProviderTest"/>类型的实例
        /// </summary>
        public WeiboAuthorizationProviderTest(ITestOutputHelper output)
        {
            _output = output;
            _provider = new WeiboAuthorizationProvider(new TestWeiboAuthorizationConfigProvider());
            MapperExtensions.SetMapper(new AutoMapperMapper());
        }

        /// <summary>
        /// 测试生成授权地址 - 默认回调
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GenerateUrlAsync_Default_Callback()
        {
            var request = new WeiboAuthorizationRequest();
            request.Init();
            var result = await _provider.GenerateUrlAsync(request);
            _output.WriteLine(result);
            Assert.Equal($"https://api.weibo.com/oauth2/authorize?client_id={TestSampleConfig.WeiboAppId}&response_type={request.ResponseType}&state={request.State}&redirect_uri={Web.UrlEncode(TestSampleConfig.WeiboCallbackUrl)}&forcelogin=false", result);
        }

        /// <summary>
        /// 测试生成授权地址 - 自定义回调
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_GenerateUrlAsync_Custom_Callback()
        {
            var request = new WeiboAuthorizationRequest();
            request.Init();
            request.RedirectUri = TestSampleConfig.WeiboCallbackUrl;
            var result = await _provider.GenerateUrlAsync(request);
            _output.WriteLine(result);
            Assert.Equal($"https://api.weibo.com/oauth2/authorize?client_id={TestSampleConfig.WeiboAppId}&response_type={request.ResponseType}&state={request.State}&redirect_uri={Web.UrlEncode(TestSampleConfig.WeiboCallbackUrl)}&forcelogin=false", result);
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
        //    param.RedirectUri = TestSampleConfig.WeiboCallbackUrl;
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
        //    var result = await _provider.GetUserInfoAsync(new WeiboAuthorizationUserRequest()
        //    {
        //        AccessToken = "",
        //        UnionId = "",
        //    });
        //    _output.WriteLine(result.ToJson());
        //    Assert.NotNull(result);
        //}
    }
}
