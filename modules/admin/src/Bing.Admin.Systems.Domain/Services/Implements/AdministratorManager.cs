using Bing.Domains.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Services.Abstractions;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 管理员 管理
    /// </summary>
    public class AdministratorManager : DomainServiceBase, IAdministratorManager
    {
        /// <summary>
        /// 初始化一个<see cref="AdministratorManager"/>类型的实例
        /// </summary>
        public AdministratorManager() { }
    }
}