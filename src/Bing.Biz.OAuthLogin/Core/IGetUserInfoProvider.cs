using System.Threading.Tasks;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 获取用户信息提供程序
    /// </summary>
    /// <typeparam name="TUserInfoResult">授权用户信息结果</typeparam>
    /// <typeparam name="TAuthorizationUserParam">授权用户参数</typeparam>
    public interface IGetUserInfoProvider<TUserInfoResult, in TAuthorizationUserParam>
        where TUserInfoResult : AuthorizationUserInfoResult
        where TAuthorizationUserParam : AuthorizationUserParamBase
    {
        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="param">授权用户参数</param>
        /// <returns></returns>
        Task<TUserInfoResult> GetUserInfoAsync(TAuthorizationUserParam param);
    }

    /// <summary>
    /// 获取用户信息提供程序
    /// </summary>
    public interface IGetUserInfoProvider : IGetUserInfoProvider<AuthorizationUserInfoResult, AuthorizationUserParam> { }
}
