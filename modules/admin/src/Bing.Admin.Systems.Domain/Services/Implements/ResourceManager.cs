using Bing.Domain.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Services.Abstractions;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 资源 管理
    /// </summary>
    public class ResourceManager : DomainServiceBase, IResourceManager
    {
        /// <summary>
        /// 初始化一个<see cref="ResourceManager"/>类型的实例
        /// </summary>
        public ResourceManager() { }
    }
}