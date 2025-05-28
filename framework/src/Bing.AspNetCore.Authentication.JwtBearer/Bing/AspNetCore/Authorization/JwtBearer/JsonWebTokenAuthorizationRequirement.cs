using Bing.Identity.JwtBearer;

namespace Bing.AspNetCore.Authorization.JwtBearer;

/// <summary>
/// JWT授权请求
/// </summary>
public class JsonWebTokenAuthorizationRequirement : IJsonWebTokenAuthorizationRequirement
{
    /// <summary>
    /// 校验负载
    /// </summary>
    protected internal Func<IDictionary<string, string>, JwtOptions, bool> ValidatePayload = (a, b) => true;

    /// <summary>
    /// 设置校验函数
    /// </summary>
    /// <param name="func">校验函数</param>
    public virtual IJsonWebTokenAuthorizationRequirement SetValidateFunc(Func<IDictionary<string, string>, JwtOptions, bool> func)
    {
        ValidatePayload = func;
        return this;
    }
}
