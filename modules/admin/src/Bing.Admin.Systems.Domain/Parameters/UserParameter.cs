using System;
using Bing.Biz.Enums;
using Bing.Domain.Services;

namespace Bing.Admin.Systems.Domain.Parameters
{
    /// <summary>
    /// 用户参数
    /// </summary>
    public class UserParameter : ParameterBase
    {
        /// <summary>
        /// 用户标识
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        public string Nickname { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 手机号
        /// </summary>
        public string PhoneNumber { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 启用
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 性别
        /// </summary>
        public Gender? Gender { get; set; }

        /// <summary>
        /// 是否系统用户
        /// </summary>
        public bool IsSystem { get; set; }
    }
}
