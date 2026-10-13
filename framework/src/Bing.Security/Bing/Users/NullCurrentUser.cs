using System.Security.Claims;

namespace Bing.Users;

/// <summary>
/// 空用户会话
/// </summary>
public class NullCurrentUser : ICurrentUser
{
    /// <summary>
    /// 空声明数组
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly Claim[] EmptyClaimsArray = new Claim[0];

    /// <summary>
    /// 空角色数组
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly string[] EmptyRolesArray = new string[0];

    /// <inheritdoc />
    public bool IsAuthenticated => false;

    /// <inheritdoc />
    public string UserId => string.Empty;

    /// <inheritdoc />
    public string UserName => string.Empty;

    /// <inheritdoc />
    public string PhoneNumber => string.Empty;

    /// <inheritdoc />
    public bool PhoneNumberVerified => false;

    /// <inheritdoc />
    public string Email => string.Empty;

    /// <inheritdoc />
    public bool EmailVerified => false;

    /// <inheritdoc />
    public string TenantId => string.Empty;

    /// <inheritdoc />
    public string[] Roles => EmptyRolesArray;

    /// <summary>
    /// 初始化一个 <see cref="NullCurrentUser"/> 类型的实例。
    /// </summary>
    private NullCurrentUser() { }

    /// <inheritdoc />
    public Claim FindClaim(string claimType) => null;

    /// <inheritdoc />
    public Claim[] FindClaims(string claimType) => EmptyClaimsArray;

    /// <inheritdoc />
    public Claim[] GetAllClaims() => EmptyClaimsArray;

    /// <inheritdoc />
    public bool IsInRole(string roleName) => false;

    /// <summary>
    /// 空用户实例
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static readonly ICurrentUser Instance = new NullCurrentUser();
}