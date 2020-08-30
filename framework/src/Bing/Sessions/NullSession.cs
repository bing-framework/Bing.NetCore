using System.Security.Claims;

namespace Bing.Sessions
{
    /// <summary>
    /// 空用户会话
    /// </summary>
    public class NullSession : ISession
    {
        /// <summary>
        /// 空声明数组
        /// </summary>
        private static readonly Claim[] EmptyClaimsArray = new Claim[0];

        /// <summary>
        /// 用户编号
        /// </summary>
        public string UserId => string.Empty;

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName => string.Empty;

        /// <summary>
        /// 是否认证
        /// </summary>
        public bool IsAuthenticated => false;

        /// <summary>
        /// 查找声明
        /// </summary>
        /// <param name="claimType">声明类型</param>
        public Claim FindClaim(string claimType) => null;

        /// <summary>
        /// 查找声明列表
        /// </summary>
        /// <param name="claimType">声明类型</param>
        public Claim[] FindClaims(string claimType) => EmptyClaimsArray;

        /// <summary>
        /// 获取所有声明列表
        /// </summary>
        public Claim[] GetAllClaims() => EmptyClaimsArray;

        /// <summary>
        /// 空用户会话实例
        /// </summary>
        public static readonly ISession Instance = new NullSession();
    }
}
