using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Commons.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Commons.Extensions {
    /// <summary>
    /// 字典数据传输对象扩展
    /// </summary>
    public static class DictionaryDtoExtension {
        /// <summary>
        /// 转换为字典实体
        /// </summary>
        /// <param name="dto">字典数据传输对象</param>
        public static Dictionary ToEntity( this DictionaryDto dto ) {
            if( dto == null )
                return new Dictionary();
            return new Dictionary( dto.Id.ToGuid(),dto.Id,1 ) {
                Code = dto.Code,
                Text = dto.Text,
                PinYin = dto.PinYin,
                ParentId = dto.ParentId.ToGuidOrNull(),
                Enabled = dto.Enabled.SafeValue(),
                SortId = dto.SortId,
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
        /// 转换为字典数据传输对象
        /// </summary>
        /// <param name="entity">字典实体</param>
        public static DictionaryDto ToDto( this Dictionary entity ) {
            if( entity == null )
                return new DictionaryDto();
            return new DictionaryDto {
                Id = entity.Id.ToString(),
                Code = entity.Code,
                Text = entity.Text,
                PinYin = entity.PinYin,
                ParentId = entity.ParentId.ToString(),
                Path = entity.Path,
                Level = entity.Level,
                Enabled = entity.Enabled,
                SortId = entity.SortId,
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