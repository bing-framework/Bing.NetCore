using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Systems.Extensions {
    /// <summary>
    /// 应用程序数据传输对象扩展
    /// </summary>
    public static class ApplicationDtoExtension {
        
        /// <summary>
        /// 转换为应用程序实体
        /// </summary>
        /// <param name="dto">应用程序数据传输对象</param>
        public static Application ToEntity( this ApplicationDto dto ) {
            if( dto == null )
                return new Application();
            return new Application( dto.Id.ToGuid() ) {
                Code = dto.Code,
                Name = dto.Name,
                Device = dto.Device,
                Note = dto.Note,
                    Enabled = dto.Enabled.SafeValue(),
                    RegisterEnabled = dto.RegisterEnabled.SafeValue(),
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
                    IsDeleted = dto.IsDeleted.SafeValue(),
                Version = dto.Version,
            };
        }
               
        /// <summary>
        /// 转换为应用程序数据传输对象
        /// </summary>
        /// <param name="entity">应用程序实体</param>
        public static ApplicationDto ToDto( this Application entity ) {
            if( entity == null )
                return new ApplicationDto();
            return new ApplicationDto {
                Id = entity.Id.ToString(),
                Code = entity.Code,
                Name = entity.Name,
                Device = entity.Device,
                Note = entity.Note,
                Enabled = entity.Enabled,
                RegisterEnabled = entity.RegisterEnabled,
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