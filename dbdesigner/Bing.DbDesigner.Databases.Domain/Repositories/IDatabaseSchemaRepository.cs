using Bing.Domains.Repositories;
using Bing.DbDesigner.Databases.Domain.Models;

namespace Bing.DbDesigner.Databases.Domain.Repositories {
    /// <summary>
    /// 数据模式仓储
    /// </summary>
    public interface IDatabaseSchemaRepository : IRepository<DatabaseSchema> {
    }
}