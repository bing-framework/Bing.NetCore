using Bing.Domain.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Services.Abstractions;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 用户角色 管理
    /// </summary>
    public class UserRoleManager : DomainServiceBase, IUserRoleManager
    {
        /// <summary>
        /// 初始化一个<see cref="UserRoleManager"/>类型的实例
        /// </summary>
        public UserRoleManager() { }
    }
}