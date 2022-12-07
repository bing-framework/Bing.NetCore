using System.Security.Claims;
using System.Threading.Tasks;

namespace Bing.Permissions.Identity.JwtBearer;

/// <summary>
/// 访问令牌用户声明提供程序
/// </summary>
public interface IAccessClaimsProvider
{
    /// <summary>
    /// 创建用户标识
    /// </summary>
    /// <param name="userId">用户标识</param>
    Task<Claim[]> CreateClaims(string userId);
}