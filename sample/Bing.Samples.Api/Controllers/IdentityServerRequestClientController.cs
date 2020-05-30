using System.Threading.Tasks;
using Bing.Helpers;
using Bing.Webs.Controllers;
using IdentityModel.Client;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Api.Controllers
{
    /// <summary>
    /// IdentityServer 请求客户端相关操作
    /// </summary>
    [Route("api/[controller]/[action]")]
    public class IdentityServerRequestClientController:ApiControllerBase
    {
        /// <summary>
        /// 授权地址
        /// </summary>
        private const string RequestUrl = "http://localhost:37680";

        /// <summary>
        /// 客户端登录
        /// </summary>
        /// <param name="scope">作用域</param>
        /// <param name="client">客户端名称</param>
        /// <param name="password">密码</param>
        /// <returns></returns>
        [HttpPost("ClientLogin")]
        public async Task<IActionResult> ClientLogin(string scope, string client, string password)
        {
            // 从元数据发现端口
            var disco = await DiscoveryClient.GetAsync(RequestUrl);
            // 请求令牌
            var tokenClient = new TokenClient(disco.TokenEndpoint, client, password);
            var tokenResponse = await tokenClient.RequestClientCredentialsAsync(scope);

            if (tokenResponse.IsError)
            {
                return Fail(tokenResponse.Error);
            }
            return Success(tokenResponse.Json);
        }

        /// <summary>
        /// 客户端密码认证登录
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="client">客户端</param>
        /// <param name="clientPassword">客户端密码</param>
        /// <param name="scope">作用域</param>
        /// <returns></returns>
        [HttpPost("ResourceOwnerPasswordLogin")]
        public async Task<IActionResult> ResourceOwnerPasswordLogin(string username, string password, string client,
            string clientPassword, string scope)
        {
            // 从元数据发现端口
            var disco = await DiscoveryClient.GetAsync(RequestUrl);
            // 请求令牌
            var tokenClient = new TokenClient(disco.TokenEndpoint, client, clientPassword);
            var tokenResponse = await tokenClient.RequestResourceOwnerPasswordAsync(username, password, scope);// 使用用户名以及密码

            if (tokenResponse.IsError)
            {
                return Fail(tokenResponse.Error);
            }
            return Success(tokenResponse.Json);
        }

        /// <summary>
        /// 获取授权服务用户信息
        /// </summary>
        /// <param name="token">令牌信息</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetIdentityServerUserInfo(string token)
        {
            var response =await Web.Client().Get("http://localhost:37680/TestIdentityServer4/Get").BearerToken(token).ResultAsync();
            return Success(response);
        }
    }
}
