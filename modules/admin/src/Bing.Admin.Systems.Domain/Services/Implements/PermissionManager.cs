using Bing.Domains.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Services.Abstractions;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 权限 管理
    /// </summary>
    public class PermissionManager : DomainServiceBase, IPermissionManager
    {
        /// <summary>
        /// 初始化一个<see cref="PermissionManager"/>类型的实例
        /// </summary>
        public PermissionManager() { }
    }
}