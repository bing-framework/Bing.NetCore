using System.Threading.Tasks;
using Bing.Domains.Repositories;
using Bing.DbDesigner.Systems.Domain.Models;

namespace Bing.DbDesigner.Systems.Domain.Repositories {
    /// <summary>
    /// 应用程序仓储
    /// </summary>
    public interface IApplicationRepository : IRepository<Application>
    {
        /// <summary>
        /// 通过应用程序编码查找
        /// </summary>
        /// <param name="code">应用程序编码</param>
        /// <returns></returns>
        Task<Application> GetByCodeAsync(string code);
    }
}