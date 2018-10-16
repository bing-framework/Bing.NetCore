using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Projects.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Projects.Extensions {
    /// <summary>
    /// 用户解决方案数据传输对象扩展
    /// </summary>
    public static class UserSolutionDtoExtension {
        /// <summary>
        /// 转换为用户解决方案实体
        /// </summary>
        /// <param name="dto">用户解决方案数据传输对象</param>
        public static UserSolution ToEntity( this UserSolutionDto dto ) {
            if( dto == null )
                return new UserSolution();
            return new UserSolution( dto.Id.ToGuid() ) {
                SolutionId = dto.SolutionId,
                UserId = dto.UserId,
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
        /// 转换为用户解决方案数据传输对象
        /// </summary>
        /// <param name="entity">用户解决方案实体</param>
        public static UserSolutionDto ToDto( this UserSolution entity ) {
            if( entity == null )
                return new UserSolutionDto();
            return new UserSolutionDto {
                Id = entity.Id.ToString(),
                SolutionId = entity.SolutionId,
                UserId = entity.UserId,
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