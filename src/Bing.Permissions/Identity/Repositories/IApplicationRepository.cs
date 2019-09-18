using Bing.Domains.Repositories;
using Bing.Permissions.Identity.Models;

namespace Bing.Permissions.Identity.Repositories
{
    /// <summary>
    /// 应用程序仓储
    /// </summary>
    /// <typeparam name="TApplication">应用程序类型</typeparam>
    /// <typeparam name="TKey">应用程序标识类型</typeparam>
    public interface IApplicationRepository<TApplication, in TKey> : IRepository<TApplication, TKey> where TApplication : ApplicationBase<TApplication, TKey>
    {
    }
}
