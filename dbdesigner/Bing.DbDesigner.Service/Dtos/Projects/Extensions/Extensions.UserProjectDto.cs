using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Projects.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Projects.Extensions {
    /// <summary>
    /// 用户项目数据传输对象扩展
    /// </summary>
    public static class UserProjectDtoExtension {
        /// <summary>
        /// 转换为用户项目实体
        /// </summary>
        /// <param name="dto">用户项目数据传输对象</param>
        public static UserProject ToEntity( this UserProjectDto dto ) {
            if( dto == null )
                return new UserProject();
            return new UserProject( dto.Id.ToGuid() ) {
                UserId = dto.UserId,
                ProjectId = dto.ProjectId,
                    IsAdmin = dto.IsAdmin.SafeValue(),
                    Enabled = dto.Enabled.SafeValue(),
                Version = dto.Version,
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
            };
        }
   
        /// <summary>
        /// 转换为用户项目数据传输对象
        /// </summary>
        /// <param name="entity">用户项目实体</param>
        public static UserProjectDto ToDto( this UserProject entity ) {
            if( entity == null )
                return new UserProjectDto();
            return new UserProjectDto {
                Id = entity.Id.ToString(),
                UserId = entity.UserId,
                ProjectId = entity.ProjectId,
                IsAdmin = entity.IsAdmin,
                Enabled = entity.Enabled,
                Version = entity.Version,
                CreationTime = entity.CreationTime,
                CreatorId = entity.CreatorId,
                LastModificationTime = entity.LastModificationTime,
                LastModifierId = entity.LastModifierId,
            };
        }
    }
}