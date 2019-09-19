using System;
using System.Collections.Generic;
using Bing.Permissions.Identity.JwtBearer;

namespace Bing.Permissions.Authorization.Middlewares
{
    /// <summary>
    /// JWT客户授权配置
    /// </summary>
    public interface IJsonWebTokenCustomerAuthorizeOption
    {
        /// <summary>
        /// 设置匿名访问路径
        /// </summary>
        /// <param name="urls">路径列表</param>
        List<string> SetAnonymousPaths(IList<string> urls);

        /// <summary>
        /// 设置校验函数
        /// </summary>
        /// <param name="func">校验函数</param>
        void SetValidateFunc(Func<IDictionary<string, string>, JwtOptions, bool> func);
    }
}
