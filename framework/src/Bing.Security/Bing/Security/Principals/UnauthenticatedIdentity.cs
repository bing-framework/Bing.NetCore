using System.Security.Claims;

namespace Bing.Security.Principals;

/// <summary>
/// 未认证的身份标识
/// </summary>
public class UnauthenticatedIdentity : ClaimsIdentity
{
    /// <inheritdoc />
    public override bool IsAuthenticated => false;

    /// <summary>
    /// 未认证的身份标识实例
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static readonly UnauthenticatedIdentity Instance = new();
}
