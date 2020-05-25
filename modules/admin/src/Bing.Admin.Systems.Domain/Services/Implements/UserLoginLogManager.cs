using Bing.Domains.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Services.Abstractions;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 用户登录日志 管理
    /// </summary>
    public class UserLoginLogManager : DomainServiceBase, IUserLoginLogManager
    {
        /// <summary>
        /// 初始化一个<see cref="UserLoginLogManager"/>类型的实例
        /// </summary>
        public UserLoginLogManager() { }
    }
}