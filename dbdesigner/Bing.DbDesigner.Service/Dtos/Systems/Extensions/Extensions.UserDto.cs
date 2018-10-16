using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Systems.Extensions {
    /// <summary>
    /// 用户数据传输对象扩展
    /// </summary>
    public static class UserDtoExtension {
        /// <summary>
        /// 转换为用户实体
        /// </summary>
        /// <param name="dto">用户数据传输对象</param>
        public static User ToEntity( this UserDto dto ) {
            if( dto == null )
                return new User();
            return new User( dto.Id.ToGuid() ) {
                UserName = dto.UserName,
                NormalizedUserName = dto.NormalizedUserName,
                Email = dto.Email,
                NormalizedEmail = dto.NormalizedEmail,
                    EmailConfirmed = dto.EmailConfirmed.SafeValue(),
                PhoneNumber = dto.PhoneNumber,
                    PhoneNumberConfirmed = dto.PhoneNumberConfirmed.SafeValue(),
                Password = dto.Password,
                PasswordHash = dto.PasswordHash,
                SafePassword = dto.SafePassword,
                SafePasswordHash = dto.SafePasswordHash,
                    TwoFactorEnabled = dto.TwoFactorEnabled.SafeValue(),
                    Enabled = dto.Enabled.SafeValue(),
                DisabledTime = dto.DisabledTime,
                    LockoutEnabled = dto.LockoutEnabled.SafeValue(),
                LockoutEnd = dto.LockoutEnd,
                AccessFailedCount = dto.AccessFailedCount,
                LoginCount = dto.LoginCount,
                RegisterIp = dto.RegisterIp,
                LastLoginTime = dto.LastLoginTime,
                LastLoginIp = dto.LastLoginIp,
                CurrentLoginTime = dto.CurrentLoginTime,
                CurrentLoginIp = dto.CurrentLoginIp,
                SecunityStamp = dto.SecunityStamp,
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
        /// 转换为用户数据传输对象
        /// </summary>
        /// <param name="entity">用户实体</param>
        public static UserDto ToDto( this User entity ) {
            if( entity == null )
                return new UserDto();
            return new UserDto {
                Id = entity.Id.ToString(),
                UserName = entity.UserName,
                NormalizedUserName = entity.NormalizedUserName,
                Email = entity.Email,
                NormalizedEmail = entity.NormalizedEmail,
                EmailConfirmed = entity.EmailConfirmed,
                PhoneNumber = entity.PhoneNumber,
                PhoneNumberConfirmed = entity.PhoneNumberConfirmed,
                Password = entity.Password,
                PasswordHash = entity.PasswordHash,
                SafePassword = entity.SafePassword,
                SafePasswordHash = entity.SafePasswordHash,
                TwoFactorEnabled = entity.TwoFactorEnabled,
                Enabled = entity.Enabled,
                DisabledTime = entity.DisabledTime,
                LockoutEnabled = entity.LockoutEnabled,
                LockoutEnd = entity.LockoutEnd,
                AccessFailedCount = entity.AccessFailedCount,
                LoginCount = entity.LoginCount,
                RegisterIp = entity.RegisterIp,
                LastLoginTime = entity.LastLoginTime,
                LastLoginIp = entity.LastLoginIp,
                CurrentLoginTime = entity.CurrentLoginTime,
                CurrentLoginIp = entity.CurrentLoginIp,
                SecunityStamp = entity.SecunityStamp,
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