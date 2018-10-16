using Bing.Applications;
using Bing.DbDesigner.Service.Dtos.Databases;
using Bing.DbDesigner.Service.Queries.Databases;

namespace Bing.DbDesigner.Service.Abstractions.Databases {
    /// <summary>
    /// 数据模式服务
    /// </summary>
    public interface IDatabaseSchemaService : ICrudService<DatabaseSchemaDto, DatabaseSchemaQuery> {
    }
}