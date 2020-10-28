using System.Security.Claims;

namespace Bing.Users
{
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

        /// <summary>
        /// 是否已认证
        /// </summary>
        public bool IsAuthenticated => false;

        /// <summary>
        /// 用户标识
        /// </summary>
        public string UserId => string.Empty;

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName => string.Empty;

        /// <summary>
        /// 手机号码
        /// </summary>
        public string PhoneNumber => string.Empty;

        /// <summary>
        /// 是否已验证手机号码
        /// </summary>
        public bool PhoneNumberVerified => false;

        /// <summary>
        /// 电子邮箱
        /// </summary>
        public string Email => string.Empty;

        /// <summary>
        /// 是否已验证邮箱
        /// </summary>
        public bool EmailVerified => false;

        /// <summary>
        /// 租户标识
        /// </summary>
        public string TenantId => string.Empty;

        /// <summary>
        /// 角色列表
        /// </summary>
        public string[] Roles => EmptyRolesArray;

        /// <summary>
        /// 初始化一个<see cref="NullCurrentUser"/>类型的实例
        /// </summary>
        private NullCurrentUser() { }

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
        /// 是否包含指定角色
        /// </summary>
        /// <param name="roleName">角色名</param>
        public bool IsInRole(string roleName) => false;

        /// <summary>
        /// 空用户实例
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public static readonly ICurrentUser Instance = new NullCurrentUser();
    }
}
