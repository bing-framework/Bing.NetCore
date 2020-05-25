using Bing.Domains.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Services.Abstractions;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 用户 管理
    /// </summary>
    public class UserManager : DomainServiceBase, IUserManager
    {
        /// <summary>
        /// 初始化一个<see cref="UserManager"/>类型的实例
        /// </summary>
        public UserManager() { }
    }
}