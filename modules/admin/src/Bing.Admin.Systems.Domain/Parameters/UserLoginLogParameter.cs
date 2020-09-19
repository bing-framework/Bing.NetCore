using System;
using Bing.Domain.Services;

namespace Bing.Admin.Systems.Domain.Parameters
{
    /// <summary>
    /// 用户登录日志 参数
    /// </summary>
    public class UserLoginLogParameter : ParameterBase
    {
        /// <summary>
        /// 用户标识
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// IP地址
        /// </summary>
        public string Ip { get; set; }

        /// <summary>
        /// 用户代理
        /// </summary>
        public string UserAgent { get; set; }
    }
}
