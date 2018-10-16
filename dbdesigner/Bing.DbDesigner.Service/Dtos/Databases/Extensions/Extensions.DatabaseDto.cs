using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Databases.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Databases.Extensions {
    /// <summary>
    /// 数据库数据传输对象扩展
    /// </summary>
    public static class DatabaseDtoExtension {

        /// <summary>
        /// 转换为数据库实体
        /// </summary>
        /// <param name="dto">数据库数据传输对象</param>
        public static Database ToEntity2( this DatabaseDto dto ) {
            if( dto == null )
                return new Database();
            return new Database( dto.Id.ToGuid() ) {
                UserId = dto.UserId,
                SolutionId = dto.SolutionId,
                ProjectId = dto.ProjectId,
                Code = dto.Code,
                Name = dto.Name,
                Addreviation = dto.Addreviation,
                DbType = dto.DbType,
                    IsDeleted = dto.IsDeleted.SafeValue(),
                Version = dto.Version,
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
                    Enabled = dto.Enabled.SafeValue(),
                SortId = dto.SortId,
                Note = dto.Note,
            };
        }

        /// <summary>
        /// 转换为数据库数据传输对象
        /// </summary>
        /// <param name="entity">数据库实体</param>
        public static DatabaseDto ToDto( this Database entity ) {
            if( entity == null )
                return new DatabaseDto();
            return new DatabaseDto {
                Id = entity.Id.ToString(),
                UserId = entity.UserId,
                SolutionId = entity.SolutionId,
                ProjectId = entity.ProjectId,
                Code = entity.Code,
                Name = entity.Name,
                Addreviation = entity.Addreviation,
                DbType = entity.DbType,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
                CreationTime = entity.CreationTime,
                CreatorId = entity.CreatorId,
                LastModificationTime = entity.LastModificationTime,
                LastModifierId = entity.LastModifierId,
                Enabled = entity.Enabled,
                SortId = entity.SortId,
                Note = entity.Note,
            };
        }
    }
}