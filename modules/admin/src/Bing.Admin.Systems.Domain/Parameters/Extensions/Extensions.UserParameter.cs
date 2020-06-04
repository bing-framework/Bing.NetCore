using System;
using Bing.Admin.Commons.Domain.Models;
using Bing.Admin.Systems.Domain.Models;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Admin.Systems.Domain.Parameters.Extensions
{
    /// <summary>
    /// 用户参数扩展
    /// </summary>
    public static class UserParameterExtensions
    {
        /// <summary>
        /// 转换为用户
        /// </summary>
        /// <param name="parameter">参数</param>
        public static User ToUser(this UserParameter parameter)
        {
            if (parameter == null)
                throw new Warning("用户参数不能为空");
            return new User
            {
                UserName = parameter.UserName,
                NormalizedUserName = parameter.UserName.ToUpper(),
                Nickname = parameter.Nickname,
                PhoneNumber = GetPhoneNumber(parameter.PhoneNumber, parameter.UserName),
                Enabled = parameter.Enabled,
                IsSystem = parameter.IsSystem,
            };
        }

        /// <summary>
        /// 获取手机号码
        /// </summary>
        /// <param name="source">源</param>
        /// <param name="other">其它号码</param>
        private static string GetPhoneNumber(string source, string other) => source.IsEmpty()
            ? Valid.IsMobileNumberSimple(other) ? other : string.Empty
            : string.Empty;

        /// <summary>
        /// 转换为用户信息
        /// </summary>
        /// <param name="parameter">参数</param>
        /// <param name="userId">用户标识</param>
        public static UserInfo ToUserInfo(this UserParameter parameter, Guid userId)
        {
            if (parameter == null)
                throw new Warning("用户参数不能为空");
            if (userId.IsEmpty())
                throw new Warning("用户标识为空");
            return new UserInfo(userId)
            {
                Name = parameter.Nickname,
                Nickname = parameter.Nickname,
                Gender = parameter.Gender,
                Phone = GetPhoneNumber(parameter.PhoneNumber, parameter.UserName)
            };
        }

        /// <summary>
        /// 转换为管理员
        /// </summary>
        /// <param name="parameter">参数</param>
        /// <param name="userId">用户标识</param>
        public static Administrator ToAdministrator(this UserParameter parameter, Guid userId)
        {
            if (parameter == null)
                throw new Warning("用户参数不能为空");
            if (userId.IsEmpty())
                throw new Warning("用户标识为空");
            return new Administrator(userId)
            {
                Name = parameter.Nickname,
                Enabled = parameter.Enabled
            };
        }
    }
}
