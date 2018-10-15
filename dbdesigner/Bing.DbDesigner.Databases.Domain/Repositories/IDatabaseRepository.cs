using Bing.Domains.Repositories;
using Bing.DbDesigner.Databases.Domain.Models;

namespace Bing.DbDesigner.Databases.Domain.Repositories {
    /// <summary>
    /// 数据库仓储
    /// </summary>
    public interface IDatabaseRepository : IRepository<Database> {
    }
}