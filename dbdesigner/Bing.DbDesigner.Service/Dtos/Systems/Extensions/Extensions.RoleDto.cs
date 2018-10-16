using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Systems.Extensions {
    /// <summary>
    /// 角色数据传输对象扩展
    /// </summary>
    public static class RoleDtoExtension {
        ///// <summary>
        ///// 转换为角色实体
        ///// </summary>
        ///// <param name="dto">角色数据传输对象</param>
        //public static Role ToEntity( this RoleDto dto ) {
        //    if( dto == null )
        //        return new Role();
        //    return new Role( dto.Id.ToGuid(),dto.Id,1 ) {
        //        Code = dto.Code,
        //        Name = dto.Name,
        //        Type = dto.Type,
        //            IsAdmin = dto.IsAdmin.SafeValue(),
        //        ParentId = dto.ParentId.ToGuidOrNull(),
        //        Enabled = dto.Enabled.SafeValue(),
        //        SortId = dto.SortId,
        //        PinYin = dto.PinYin,
        //        Sign = dto.Sign,
        //        CreationTime = dto.CreationTime,
        //        CreatorId = dto.CreatorId,
        //        LastModificationTime = dto.LastModificationTime,
        //        LastModifierId = dto.LastModifierId,
        //            IsDeleted = dto.IsDeleted.SafeValue(),
        //        Version = dto.Version,
        //    };
        //}

        /// <summary>
        /// 转换为角色数据传输对象
        /// </summary>
        /// <param name="entity">角色实体</param>
        public static RoleDto ToDto( this Role entity ) {
            if( entity == null )
                return new RoleDto();
            return new RoleDto {
                Id = entity.Id.ToString(),
                Code = entity.Code,
                Name = entity.Name,
                Type = entity.Type,
                IsAdmin = entity.IsAdmin,
                ParentId = entity.ParentId.ToString(),
                Path = entity.Path,
                Level = entity.Level,
                Enabled = entity.Enabled,
                SortId = entity.SortId,
                PinYin = entity.PinYin,
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