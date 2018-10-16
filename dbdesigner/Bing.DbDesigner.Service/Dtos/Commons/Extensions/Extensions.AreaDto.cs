using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Commons.Domain.Models;
using Bing.DbDesigner.Commons.Domain.Repositories;
using Bing.Helpers;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Commons.Extensions {
    /// <summary>
    /// 地区数据传输对象扩展
    /// </summary>
    public static class AreaDtoExtension {
        /// <summary>
        /// 转换为地区实体
        /// </summary>
        /// <param name="dto">地区数据传输对象</param>
        public static Area ToEntity( this AreaDto dto ) {
            if (dto == null)
                return new Area();
            return new Area(dto.Id.ToGuid(),dto.Id,1)
            {
                Name = dto.Name,
                AdministrativeRegion = dto.AdministrativeRegion.SafeValue(),
                TelCode = dto.TelCode,
                ZipCode = dto.ZipCode,
                Longitude = dto.Longitude,
                Latitude = dto.Latitude,
                ParentId = dto.ParentId.ToGuidOrNull(),
                Enabled = dto.Enabled.SafeValue(),
                SortId = dto.SortId,
                PinYin = dto.PinYin,
                FullPinYin = dto.FullPinYin,
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
                IsDeleted = dto.IsDeleted.SafeValue(),
                Version = dto.Version,
            };
        }

        /// <summary>
        /// 转换为地区数据传输对象
        /// </summary>
        /// <param name="entity">地区实体</param>
        public static AreaDto ToDto( this Area entity ) {
            if( entity == null )
                return new AreaDto();
            var result= new AreaDto {
                Id = entity.Id.ToString(),
                Name = entity.Name,
                AdministrativeRegion = entity.AdministrativeRegion,
                TelCode = entity.TelCode,
                ZipCode = entity.ZipCode,
                Longitude = entity.Longitude,
                Latitude = entity.Latitude,
                ParentId = entity.ParentId.ToString(),
                Path = entity.Path,
                Level = entity.Level,
                Enabled = entity.Enabled,
                SortId = entity.SortId,
                PinYin = entity.PinYin,
                FullPinYin = entity.FullPinYin,
                CreationTime = entity.CreationTime,
                CreatorId = entity.CreatorId,
                LastModificationTime = entity.LastModificationTime,
                LastModifierId = entity.LastModifierId,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
            };
            if (!string.IsNullOrWhiteSpace(result.ParentId))
            {
                var dictionaryRepository = Ioc.Create<IAreaRepository>();
                var parent = dictionaryRepository.Find(entity.ParentId);
                result.ParentName = parent.Name;
            }

            return result;
        }
    }
}