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
    /// <param name="func">接收 JWT 负载和配置并返回是否通过校验的委托。</param>
    /// <returns>返回当前授权要求实例。</returns>
    public virtual IJsonWebTokenAuthorizationRequirement SetValidateFunc(Func<IDictionary<string, string>, JwtOptions, bool> func)
    {
        ValidatePayload = func;
        return this;
    }
}
