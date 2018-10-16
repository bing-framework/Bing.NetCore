using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Databases.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Databases.Extensions {
    /// <summary>
    /// 数据模式数据传输对象扩展
    /// </summary>
    public static class DatabaseSchemaDtoExtension {
        
        /// <summary>
        /// 转换为数据模式实体
        /// </summary>
        /// <param name="dto">数据模式数据传输对象</param>
        public static DatabaseSchema ToEntity( this DatabaseSchemaDto dto ) {
            if( dto == null )
                return new DatabaseSchema();
            return new DatabaseSchema( dto.Id.ToGuid() ) {
                SolutionId = dto.SolutionId,
                ProjectId = dto.ProjectId,
                DatabaseId = dto.DatabaseId,
                UserId = dto.UserId,
                Code = dto.Code,
                Name = dto.Name,
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
        /// 转换为数据模式数据传输对象
        /// </summary>
        /// <param name="entity">数据模式实体</param>
        public static DatabaseSchemaDto ToDto( this DatabaseSchema entity ) {
            if( entity == null )
                return new DatabaseSchemaDto();
            return new DatabaseSchemaDto {
                Id = entity.Id.ToString(),
                SolutionId = entity.SolutionId,
                ProjectId = entity.ProjectId,
                DatabaseId = entity.DatabaseId,
                UserId = entity.UserId,
                Code = entity.Code,
                Name = entity.Name,
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