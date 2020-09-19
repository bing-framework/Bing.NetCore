using Bing.Domain.Services;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Services.Abstractions;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// 应用程序 管理
    /// </summary>
    public class ApplicationManager : DomainServiceBase, IApplicationManager
    {
        /// <summary>
        /// 初始化一个<see cref="ApplicationManager"/>类型的实例
        /// </summary>
        public ApplicationManager() { }
    }
}