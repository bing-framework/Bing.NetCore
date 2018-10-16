using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Commons.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Commons.Extensions {
    /// <summary>
    /// 系统配置数据传输对象扩展
    /// </summary>
    public static class ConfigurationDtoExtension {
        /// <summary>
        /// 转换为系统配置实体
        /// </summary>
        /// <param name="dto">系统配置数据传输对象</param>
        public static Configuration ToEntity( this ConfigurationDto dto ) {
            if (dto == null)
                return new Configuration();
            return new Configuration(dto.Id.ToGuid())
            {
                Name = dto.Name,
                Code = dto.Code,
                Value = dto.Value,
                IsDeleted = dto.IsDeleted.SafeValue(),
                Version = dto.Version,
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
                Enabled = dto.Enabled.SafeValue(),
                Note = dto.Note,
                TenantId = dto.TenantId,
            };
        }        
        
        /// <summary>
        /// 转换为系统配置数据传输对象
        /// </summary>
        /// <param name="entity">系统配置实体</param>
        public static ConfigurationDto ToDto( this Configuration entity ) {
            if( entity == null )
                return new ConfigurationDto();
            return new ConfigurationDto {
                Id = entity.Id.ToString(),
                Name = entity.Name,
                Code = entity.Code,
                Value = entity.Value,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
                CreationTime = entity.CreationTime,
                CreatorId = entity.CreatorId,
                LastModificationTime = entity.LastModificationTime,
                LastModifierId = entity.LastModifierId,
                Enabled = entity.Enabled,
                Note = entity.Note,
                TenantId = entity.TenantId,
            };
        }
    }
}