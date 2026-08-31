using Bing.Identity.JwtBearer;

namespace Bing.AspNetCore.Authorization.JwtBearer;

/// <summary>
/// 配置 JWT 客户端授权中的匿名路径和令牌负载校验策略。
/// </summary>
public class JsonWebTokenCustomerAuthorizeOption : IJsonWebTokenCustomerAuthorizeOption
{
    /// <summary>
    /// 保存授权中间件直接放行的匿名访问路径。
    /// </summary>
    protected internal readonly List<string> AnonymousPaths = new List<string>();

    /// <summary>
    /// 保存 JWT 负载校验委托；委托返回 <see langword="true"/> 时表示负载校验通过。
    /// </summary>
    protected internal Func<IDictionary<string, string>, JwtOptions, bool> ValidatePayload = (a, b) => true;

    /// <summary>
    /// 初始化 <see cref="JsonWebTokenCustomerAuthorizeOption"/> 的实例及默认授权策略。
    /// </summary>
    public JsonWebTokenCustomerAuthorizeOption() { }

    /// <summary>
    /// 将指定路径追加到匿名访问路径列表。
    /// </summary>
    /// <param name="urls">要追加的匿名访问路径列表。</param>
    /// <returns>更新后的匿名访问路径列表。</returns>
    public List<string> SetAnonymousPaths(IList<string> urls)
    {
        urls.ToList().ForEach(url =>
        {
            AnonymousPaths.Add(url);
        });
        return AnonymousPaths;
    }

    /// <summary>
    /// 设置 JWT 负载校验委托。
    /// </summary>
    /// <param name="func">接收负载和 JWT 配置并返回校验结果的委托。</param>
    public void SetValidateFunc(Func<IDictionary<string, string>, JwtOptions, bool> func) => ValidatePayload = func;
}
