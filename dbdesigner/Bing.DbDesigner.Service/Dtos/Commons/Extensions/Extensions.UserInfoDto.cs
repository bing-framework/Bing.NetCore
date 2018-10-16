using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Commons.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Commons.Extensions {
    /// <summary>
    /// 用户信息数据传输对象扩展
    /// </summary>
    public static class UserInfoDtoExtension {
        
        /// <summary>
        /// 转换为用户信息实体
        /// </summary>
        /// <param name="dto">用户信息数据传输对象</param>
        public static UserInfo ToEntity( this UserInfoDto dto ) {
            if( dto == null )
                return new UserInfo();
            return new UserInfo( dto.Id.ToGuid() ) {
                Code = dto.Code,
                Name = dto.Name,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                NickName = dto.NickName,
                EnglishName = dto.EnglishName,
                Gender = dto.Gender,
                Birthday = dto.Birthday,
                NativePlace = dto.NativePlace,
                Nation = dto.Nation,
                Phone = dto.Phone,
                Email = dto.Email,
                Qq = dto.Qq,
                Wechat = dto.Wechat,
                Fax = dto.Fax,
                ProvinceId = dto.ProvinceId,
                Province = dto.Province,
                CityId = dto.CityId,
                City = dto.City,
                CountyId = dto.CountyId,
                County = dto.County,
                Street = dto.Street,
                Zip = dto.Zip,
                IdCard = dto.IdCard,
                Avatar = dto.Avatar,
                IdCardFront = dto.IdCardFront,
                IdCardReverse = dto.IdCardReverse,
                Degree = dto.Degree,
                SchoolOfGraduate = dto.SchoolOfGraduate,
                Discipline = dto.Discipline,
                Company = dto.Company,
                Resume = dto.Resume,
                PinYin = dto.PinYin,
                Note = dto.Note,
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
                TenantId = dto.TenantId,
                    IsDeleted = dto.IsDeleted.SafeValue(),
                Version = dto.Version,
            };
        }

        /// <summary>
        /// 转换为用户信息数据传输对象
        /// </summary>
        /// <param name="entity">用户信息实体</param>
        public static UserInfoDto ToDto( this UserInfo entity ) {
            if( entity == null )
                return new UserInfoDto();
            return new UserInfoDto {
                Id = entity.Id.ToString(),
                Code = entity.Code,
                Name = entity.Name,
                FirstName = entity.FirstName,
                LastName = entity.LastName,
                NickName = entity.NickName,
                EnglishName = entity.EnglishName,
                Gender = entity.Gender,
                Birthday = entity.Birthday,
                NativePlace = entity.NativePlace,
                Nation = entity.Nation,
                Phone = entity.Phone,
                Email = entity.Email,
                Qq = entity.Qq,
                Wechat = entity.Wechat,
                Fax = entity.Fax,
                ProvinceId = entity.ProvinceId,
                Province = entity.Province,
                CityId = entity.CityId,
                City = entity.City,
                CountyId = entity.CountyId,
                County = entity.County,
                Street = entity.Street,
                Zip = entity.Zip,
                IdCard = entity.IdCard,
                Avatar = entity.Avatar,
                IdCardFront = entity.IdCardFront,
                IdCardReverse = entity.IdCardReverse,
                Degree = entity.Degree,
                SchoolOfGraduate = entity.SchoolOfGraduate,
                Discipline = entity.Discipline,
                Company = entity.Company,
                Resume = entity.Resume,
                PinYin = entity.PinYin,
                Note = entity.Note,
                CreationTime = entity.CreationTime,
                CreatorId = entity.CreatorId,
                LastModificationTime = entity.LastModificationTime,
                LastModifierId = entity.LastModifierId,
                TenantId = entity.TenantId,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
            };
        }
    }
}