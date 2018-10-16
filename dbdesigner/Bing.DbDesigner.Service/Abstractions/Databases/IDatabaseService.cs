using Bing.Applications;
using Bing.DbDesigner.Service.Dtos.Databases;
using Bing.DbDesigner.Service.Queries.Databases;

namespace Bing.DbDesigner.Service.Abstractions.Databases {
    /// <summary>
    /// 数据库服务
    /// </summary>
    public interface IDatabaseService : ICrudService<DatabaseDto, DatabaseQuery> {
    }
}