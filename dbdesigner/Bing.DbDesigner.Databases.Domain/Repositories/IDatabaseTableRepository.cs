using Bing.Domains.Repositories;
using Bing.DbDesigner.Databases.Domain.Models;

namespace Bing.DbDesigner.Databases.Domain.Repositories {
    /// <summary>
    /// 数据表仓储
    /// </summary>
    public interface IDatabaseTableRepository : IRepository<DatabaseTable> {
    }
}