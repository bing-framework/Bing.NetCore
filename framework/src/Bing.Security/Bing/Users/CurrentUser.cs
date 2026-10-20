using System.Security.Claims;
using System.Security.Principal;
using Bing.DependencyInjection;
using Bing.Security.Claims;

namespace Bing.Users;

/// <summary>
/// 当前用户
/// </summary>
public class CurrentUser : ICurrentUser, ITransientDependency
{
    /// <summary>
    /// 空声明数组
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly Claim[] EmptyClaimsArray = Array.Empty<Claim>();

    /// <summary>
    /// 安全主体访问器
    /// </summary>
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    /// <summary>
    /// 初始化一个 <see cref="CurrentUser"/> 类型的实例。
    /// </summary>
    /// <param name="principalAccessor">安全主体访问器</param>
    public CurrentUser(ICurrentPrincipalAccessor principalAccessor) => _principalAccessor = principalAccessor;

    /// <inheritdoc />
    public virtual bool IsAuthenticated => !string.IsNullOrWhiteSpace(UserId);

    /// <inheritdoc />
    public virtual string UserId
    {
        get
        {
            var result = this.FindClaimValue(BingClaimTypes.UserId);
            if (string.IsNullOrWhiteSpace(result))
                result = this.FindClaimValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(result))
                result = this.FindClaimValue("sid");
            if (string.IsNullOrWhiteSpace(result))
                result = this.FindClaimValue("sub");
            return result;
        }
    }

    /// <inheritdoc />
    public virtual string UserName
    {
        get
        {
            var result = this.FindClaimValue(BingClaimTypes.UserName);
            if (string.IsNullOrWhiteSpace(result))
                result = this.FindClaimValue("name");
            return result;
        }
    }

    /// <inheritdoc />
    public virtual string PhoneNumber => this.FindClaimValue(BingClaimTypes.PhoneNumber);

    /// <inheritdoc />
    public virtual bool PhoneNumberVerified => string.Equals(this.FindClaimValue(BingClaimTypes.PhoneNumberVerified), "true",
        StringComparison.InvariantCultureIgnoreCase);

    /// <inheritdoc />
    public virtual string Email => this.FindClaimValue(BingClaimTypes.Email);

    /// <inheritdoc />
    public virtual bool EmailVerified => string.Equals(this.FindClaimValue(BingClaimTypes.EmailVerified), "true",
        StringComparison.InvariantCultureIgnoreCase);

    /// <inheritdoc />
    public virtual string TenantId => _principalAccessor.Principal?.FindTenantId()?.ToString();

    /// <inheritdoc />
    public virtual string[] Roles
    {
        get
        {
            var result = FindClaims(BingClaimTypes.Role).Select(x => x.Value).ToArray();
            if (!result.Any())
                result = FindClaims("role").Select(x => x.Value).ToArray();
            return result;
        }
    }

    /// <inheritdoc />
    public virtual Claim FindClaim(string claimType) => _principalAccessor.Principal?.Claims.FirstOrDefault(c => c.Type == claimType);

    /// <inheritdoc />
    public virtual Claim[] FindClaims(string claimType) => _principalAccessor.Principal?.Claims.Where(c => c.Type == claimType).ToArray() ?? EmptyClaimsArray;

    /// <inheritdoc />
    public virtual Claim[] GetAllClaims() => _principalAccessor.Principal?.Claims.ToArray() ?? EmptyClaimsArray;

    /// <inheritdoc />
    public virtual bool IsInRole(string roleName) => FindClaims(BingClaimTypes.Role).Any(c => c.Value == roleName);
}
