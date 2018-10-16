using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Commons.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Commons.Extensions {
    /// <summary>
    /// 文件数据传输对象扩展
    /// </summary>
    public static class FileDtoExtension {
        /// <summary>
        /// 转换为文件实体
        /// </summary>
        /// <param name="dto">文件数据传输对象</param>
        public static File ToEntity( this FileDto dto ) {
            if( dto == null )
                return new File();
            return new File( dto.Id.ToGuid() ) {
                Name = dto.Name,
                Size = dto.Size,
                SizeExplain = dto.SizeExplain,
                Extensions = dto.Extensions,
                Address = dto.Address,
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                Version = dto.Version,
            };
        }

        /// <summary>
        /// 转换为文件数据传输对象
        /// </summary>
        /// <param name="entity">文件实体</param>
        public static FileDto ToDto( this File entity ) {
            if( entity == null )
                return new FileDto();
            return new FileDto {
                Id = entity.Id.ToString(),
                Name = entity.Name,
                Size = entity.Size,
                SizeExplain = entity.SizeExplain,
                Extensions = entity.Extensions,
                Address = entity.Address,
                CreationTime = entity.CreationTime,
                CreatorId = entity.CreatorId,
                Version = entity.Version,
            };
        }
    }
}