using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Databases.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Databases.Extensions {
    /// <summary>
    /// 用户数据库数据传输对象扩展
    /// </summary>
    public static class UserDatabaseDtoExtension {
        /// <summary>
        /// 转换为用户数据库实体
        /// </summary>
        /// <param name="dto">用户数据库数据传输对象</param>
        public static UserDatabase ToEntity( this UserDatabaseDto dto ) {
            if( dto == null )
                return new UserDatabase();
            return new UserDatabase( dto.Id.ToGuid() ) {
                UserId = dto.UserId,
                DatabaseId = dto.DatabaseId,
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
        /// 转换为用户数据库数据传输对象
        /// </summary>
        /// <param name="entity">用户数据库实体</param>
        public static UserDatabaseDto ToDto2( this UserDatabase entity ) {
            if( entity == null )
                return new UserDatabaseDto();
            return new UserDatabaseDto {
                Id = entity.Id.ToString(),
                UserId = entity.UserId,
                DatabaseId = entity.DatabaseId,
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