using Bing.Domains.Repositories;
using Bing.DbDesigner.Commons.Domain.Models;

namespace Bing.DbDesigner.Commons.Domain.Repositories {
    /// <summary>
    /// 系统配置仓储
    /// </summary>
    public interface IConfigurationRepository : IRepository<Models.Configuration> {
    }
}