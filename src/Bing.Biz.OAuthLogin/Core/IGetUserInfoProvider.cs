using System.Threading.Tasks;

namespace Bing.Biz.OAuthLogin.Core
{
    /// <summary>
    /// 获取用户信息提供程序
    /// </summary>
    /// <typeparam name="TUserInfoResult">授权用户信息结果</typeparam>
    public interface IGetUserInfoProvider<TUserInfoResult> where TUserInfoResult : AuthorizationUserInfoResult
    {
        Task<TUserInfoResult> GetUserInfo();
    }

    /// <summary>
    /// 获取用户信息提供程序
    /// </summary>
    public interface IGetUserInfoProvider : IGetUserInfoProvider<AuthorizationUserInfoResult> { }
}
