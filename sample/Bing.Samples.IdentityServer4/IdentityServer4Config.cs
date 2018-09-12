using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Models;

namespace Bing.Samples.IdentityServer4
{
    /// <summary>
    /// IdentityServer4 配置
    /// </summary>
    public class IdentityServer4Config
    {
        /// <summary>
        /// 获取授权资源列表
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<IdentityResource> GetIdentityResources()
        {
            return new List<IdentityResource>()
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
                new IdentityResources.Email(),
                new IdentityResources.Phone()
            };
        }

        /// <summary>
        /// 获取API资源列表
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<ApiResource> GetApiResources()
        {
            return new List<ApiResource>()
            {
                new ApiResource("api","API"),
                new ApiResource("sample_api","案例API")
            };
        }

        /// <summary>
        /// 令牌有效时间，单位：秒
        /// </summary>
        public const int AccessTokenLifetime = 900000;

        /// <summary>
        /// 获取访问客户端列表
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Client> GetClients()
        {
            return new List<Client>()
            {
                new Client()
                {
                    ClientId = "sample.admin",
                    ClientName = "后台管理系统",
                    AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                    ClientSecrets = {new Secret("123456".Sha256())},
                    AllowedScopes =
                    {
                        "api",
                        "sample_api"
                    },
                    AccessTokenLifetime = AccessTokenLifetime
                }
            };
        }
    }
}
