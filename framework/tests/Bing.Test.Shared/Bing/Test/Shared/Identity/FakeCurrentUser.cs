using System.Security.Claims;
using Bing.Users;

namespace Bing.Test.Shared.Identity;

/// <summary>
/// 测试专用伪用户，可精确控制当前用户上下文，消除对真实 HttpContext / Claims 的依赖。
/// 用法示例：
/// <code>
///   var user = FakeCurrentUser.AsAuthenticated("user-001", "张三");
///   Assert.True(user.IsAuthenticated);
///   Assert.Equal("user-001", user.UserId);
/// </code>
/// </summary>
public class FakeCurrentUser : ICurrentUser
{
    private static readonly Claim[] EmptyClaimsArray = Array.Empty<Claim>();
    private static readonly string[] EmptyRolesArray = Array.Empty<string>();

    private readonly List<Claim> _claims;

    /// <inheritdoc/>
    public bool IsAuthenticated { get; set; }

    /// <inheritdoc/>
    public string UserId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string UserName { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <inheritdoc/>
    public bool PhoneNumberVerified { get; set; }

    /// <inheritdoc/>
    public string Email { get; set; } = string.Empty;

    /// <inheritdoc/>
    public bool EmailVerified { get; set; }

    /// <inheritdoc/>
    public string TenantId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string[] Roles { get; set; } = EmptyRolesArray;

    /// <summary>
    /// 初始化一个未认证的空用户实例
    /// </summary>
    public FakeCurrentUser()
    {
        _claims = new List<Claim>();
        IsAuthenticated = false;
    }

    /// <summary>
    /// 初始化一个带有指定属性的用户实例
    /// </summary>
    private FakeCurrentUser(string userId, string userName, string tenantId, bool isAuthenticated, string[] roles, IEnumerable<Claim> extraClaims)
    {
        UserId = userId ?? string.Empty;
        UserName = userName ?? string.Empty;
        TenantId = tenantId ?? string.Empty;
        IsAuthenticated = isAuthenticated;
        Roles = roles ?? EmptyRolesArray;
        _claims = new List<Claim>(extraClaims ?? Enumerable.Empty<Claim>());
    }

    /// <summary>
    /// 创建一个已认证的用户实例（最常用的工厂方法）
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="userName">用户名</param>
    /// <param name="tenantId">租户标识（可选）</param>
    /// <param name="roles">角色列表（可选）</param>
    public static FakeCurrentUser AsAuthenticated(string userId = "test-user-id", string userName = "test-user",
        string tenantId = null, string[] roles = null)
    {
        return new FakeCurrentUser(userId, userName, tenantId, true, roles, null);
    }

    /// <summary>
    /// 创建一个未认证的匿名用户实例
    /// </summary>
    public static FakeCurrentUser AsAnonymous() => new FakeCurrentUser();

    /// <summary>
    /// 添加额外声明（支持链式调用）
    /// </summary>
    /// <param name="type">声明类型</param>
    /// <param name="value">声明值</param>
    public FakeCurrentUser WithClaim(string type, string value)
    {
        _claims.Add(new Claim(type, value));
        return this;
    }

    /// <inheritdoc/>
    public Claim FindClaim(string claimType) =>
        _claims.FirstOrDefault(c => c.Type == claimType);

    /// <inheritdoc/>
    public Claim[] FindClaims(string claimType) =>
        _claims.Where(c => c.Type == claimType).ToArray();

    /// <inheritdoc/>
    public Claim[] GetAllClaims() => _claims.ToArray();

    /// <inheritdoc/>
    public bool IsInRole(string roleName) =>
        Roles != null && Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
}
