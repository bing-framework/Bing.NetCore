using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Systems.Extensions {
    /// <summary>
    /// 权限数据传输对象扩展
    /// </summary>
    public static class PermissionDtoExtension {

        /// <summary>
        /// 转换为权限实体
        /// </summary>
        /// <param name="dto">权限数据传输对象</param>
        public static Permission ToEntity( this PermissionDto dto ) {
            if( dto == null )
                return new Permission();
            return new Permission( dto.Id.ToGuid() ) {
                RoleId = dto.RoleId,
                ResourceId = dto.ResourceId,
                    IsDeny = dto.IsDeny.SafeValue(),
                Sign = dto.Sign,
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
                    IsDeleted = dto.IsDeleted.SafeValue(),
                Version = dto.Version,
            };
        }

        /// <summary>
        /// 转换为权限数据传输对象
        /// </summary>
        /// <param name="entity">权限实体</param>
        public static PermissionDto ToDto( this Permission entity ) {
            if( entity == null )
                return new PermissionDto();
            return new PermissionDto {
                Id = entity.Id.ToString(),
                RoleId = entity.RoleId,
                ResourceId = entity.ResourceId,
                IsDeny = entity.IsDeny,
                Sign = entity.Sign,
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