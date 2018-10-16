using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Projects.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Projects.Extensions {
    /// <summary>
    /// 项目数据传输对象扩展
    /// </summary>
    public static class ProjectDtoExtension {
        /// <summary>
        /// 转换为项目实体
        /// </summary>
        /// <param name="dto">项目数据传输对象</param>
        public static Project ToEntity( this ProjectDto dto ) {
            if( dto == null )
                return new Project();
            return new Project( dto.Id.ToGuid() ) {
                SolutionId = dto.SolutionId,
                UserId = dto.UserId,
                Code = dto.Code,
                Name = dto.Name,
                Addreviation = dto.Addreviation,
                AuthCode = dto.AuthCode,
                AuthKey = dto.AuthKey,
                    Enabled = dto.Enabled.SafeValue(),
                SortId = dto.SortId,
                Note = dto.Note,
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
                    IsDeleted = dto.IsDeleted.SafeValue(),
                Version = dto.Version,
            };
        }
        
        /// <summary>
        /// 转换为项目数据传输对象
        /// </summary>
        /// <param name="entity">项目实体</param>
        public static ProjectDto ToDto( this Project entity ) {
            if( entity == null )
                return new ProjectDto();
            return new ProjectDto {
                Id = entity.Id.ToString(),
                SolutionId = entity.SolutionId,
                UserId = entity.UserId,
                Code = entity.Code,
                Name = entity.Name,
                Addreviation = entity.Addreviation,
                AuthCode = entity.AuthCode,
                AuthKey = entity.AuthKey,
                Enabled = entity.Enabled,
                SortId = entity.SortId,
                Note = entity.Note,
                CreationTime = entity.CreationTime,
                CreatorId = entity.CreatorId,
                LastModificationTime = entity.LastModificationTime,
                LastModifierId = entity.LastModifierId,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
            };
        }
    }
}