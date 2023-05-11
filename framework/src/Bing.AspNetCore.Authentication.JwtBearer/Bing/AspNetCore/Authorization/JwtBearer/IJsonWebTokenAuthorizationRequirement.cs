using Bing.Identity.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace Bing.AspNetCore.Authorization.JwtBearer;

/// <summary>
/// JWT授权请求
/// </summary>
public interface IJsonWebTokenAuthorizationRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// 设置校验函数
    /// </summary>
    /// <param name="func">校验函数</param>
    IJsonWebTokenAuthorizationRequirement SetValidateFunc(
        Func<IDictionary<string, string>, JwtOptions, bool> func);
}
