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
    /// 初始化一个 <see cref="JsonWebTokenCustomerAuthorizeOption"/> 类型的实例。
    /// </summary>
    /// <remarks>同时创建匿名路径列表并启用默认负载校验策略。</remarks>
    public JsonWebTokenCustomerAuthorizeOption() { }

    /// <inheritdoc />
    /// <remarks>
    /// 将指定路径追加到匿名访问路径列表。
    /// </remarks>
    public List<string> SetAnonymousPaths(IList<string> urls)
    {
        urls.ToList().ForEach(url =>
        {
            AnonymousPaths.Add(url);
        });
        return AnonymousPaths;
    }

    /// <inheritdoc />
    public void SetValidateFunc(Func<IDictionary<string, string>, JwtOptions, bool> func) => ValidatePayload = func;
}
