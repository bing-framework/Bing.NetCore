using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Databases.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Databases.Extensions {
    /// <summary>
    /// 数据表数据传输对象扩展
    /// </summary>
    public static class DatabaseTableDtoExtension {
        /// <summary>
        /// 转换为数据表实体
        /// </summary>
        /// <param name="dto">数据表数据传输对象</param>
        public static DatabaseTable ToEntity( this DatabaseTableDto dto ) {
            if( dto == null )
                return new DatabaseTable();
            return new DatabaseTable(dto.Id.ToGuid())
            {
                SolutionId = dto.SolutionId,
                ProjectId = dto.ProjectId,
                DatabaseId = dto.DatabaseId,
                DatabaseSchemaId = dto.DatabaseSchemaId,
                UserId = dto.UserId,
                Code = dto.Code,
                Name = dto.Name,
                Comment = dto.Comment,
                IsDeleted = dto.IsDeleted.SafeValue(),
                Enabled = dto.Enabled.SafeValue(),
                SortId = dto.SortId,
                Note = dto.Note,
                Extend = dto.Extend,
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
                Version = dto.Version,
            };
        }
 
        /// <summary>
        /// 转换为数据表数据传输对象
        /// </summary>
        /// <param name="entity">数据表实体</param>
        public static DatabaseTableDto ToDto( this DatabaseTable entity ) {
            if( entity == null )
                return new DatabaseTableDto();
            return new DatabaseTableDto {
                Id = entity.Id.ToString(),
                SolutionId = entity.SolutionId,
                ProjectId = entity.ProjectId,
                DatabaseId = entity.DatabaseId,
                DatabaseSchemaId = entity.DatabaseSchemaId,
                UserId = entity.UserId,
                Code = entity.Code,
                Name = entity.Name,
                Comment = entity.Comment,
                IsDeleted = entity.IsDeleted,
                Enabled = entity.Enabled,
                SortId = entity.SortId,
                Note = entity.Note,
                Extend = entity.Extend,
                CreationTime = entity.CreationTime,
                CreatorId = entity.CreatorId,
                LastModificationTime = entity.LastModificationTime,
                LastModifierId = entity.LastModifierId,
                Version = entity.Version,
            };
        }
    }
}