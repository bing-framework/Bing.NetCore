using Bing.Applications;
using Bing.DbDesigner.Service.Dtos.Commons;
using Bing.DbDesigner.Service.Queries.Commons;

namespace Bing.DbDesigner.Service.Abstractions.Commons {
    /// <summary>
    /// 文件服务
    /// </summary>
    public interface IFileService : ICrudService<FileDto, FileQuery> {
    }
}