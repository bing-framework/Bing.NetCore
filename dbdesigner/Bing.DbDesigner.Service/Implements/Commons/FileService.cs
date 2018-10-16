using Bing;
using Bing.Extensions.AutoMapper;
using Bing.Domains.Repositories;
using Bing.Datas.Queries;
using Bing.Applications;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Commons.Domain.Models;
using Bing.DbDesigner.Commons.Domain.Repositories;
using Bing.DbDesigner.Service.Dtos.Commons;
using Bing.DbDesigner.Service.Queries.Commons;
using Bing.DbDesigner.Service.Abstractions.Commons;

namespace Bing.DbDesigner.Service.Implements.Commons {
    /// <summary>
    /// 文件服务
    /// </summary>
    public class FileService : CrudServiceBase<File, FileDto, FileQuery>, IFileService {
        /// <summary>
        /// 初始化文件服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="fileRepository">文件仓储</param>
        public FileService( IDbDesignerUnitOfWork unitOfWork, IFileRepository fileRepository )
            : base( unitOfWork, fileRepository ) {
            FileRepository = fileRepository;
        }
        
        /// <summary>
        /// 文件仓储
        /// </summary>
        public IFileRepository FileRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<File> CreateQuery( FileQuery param ) {
            return new Query<File>( param );
        }
    }
}