using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Systems.Extensions {
    /// <summary>
    /// 资源数据传输对象扩展
    /// </summary>
    public static class ResourceDtoExtension {
        /// <summary>
        /// 转换为资源实体
        /// </summary>
        /// <param name="dto">资源数据传输对象</param>
        public static Resource ToEntity2( this ResourceDto dto ) {
            if( dto == null )
                return new Resource();
            return new Resource( dto.Id.ToGuid(),dto.Id,1 ) {
                ApplicationId = dto.ApplicationId,
                Uri = dto.Uri,
                Name = dto.Name,
                Type = dto.Type,
                CreationTime = dto.CreationTime,
                ParentId = dto.ParentId.ToGuidOrNull(),
                Enabled = dto.Enabled.SafeValue(),
                SortId = dto.SortId,
                Note = dto.Note,
                PinYin = dto.PinYin,
                Extend = dto.Extend,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
                    IsDeleted = dto.IsDeleted.SafeValue(),
                Version = dto.Version,
            };
        }

        /// <summary>
        /// 转换为资源数据传输对象
        /// </summary>
        /// <param name="entity">资源实体</param>
        public static ResourceDto ToDto( this Resource entity ) {
            if( entity == null )
                return new ResourceDto();
            return new ResourceDto {
                Id = entity.Id.ToString(),
                ApplicationId = entity.ApplicationId,
                Uri = entity.Uri,
                Name = entity.Name,
                Type = entity.Type,
                CreationTime = entity.CreationTime,
                ParentId = entity.ParentId.ToString(),
                Path = entity.Path,
                Level = entity.Level,
                Enabled = entity.Enabled,
                SortId = entity.SortId,
                Note = entity.Note,
                PinYin = entity.PinYin,
                Extend = entity.Extend,
                CreatorId = entity.CreatorId,
                LastModificationTime = entity.LastModificationTime,
                LastModifierId = entity.LastModifierId,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
            };
        }
    }
}