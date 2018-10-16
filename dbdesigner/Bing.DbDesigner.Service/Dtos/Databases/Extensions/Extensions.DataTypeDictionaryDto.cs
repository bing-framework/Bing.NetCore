using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Databases.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Databases.Extensions {
    /// <summary>
    /// 数据类型字典数据传输对象扩展
    /// </summary>
    public static class DataTypeDictionaryDtoExtension {
        /// <summary>
        /// 转换为数据类型字典实体
        /// </summary>
        /// <param name="dto">数据类型字典数据传输对象</param>
        public static DataTypeDictionary ToEntity( this DataTypeDictionaryDto dto ) {
            if( dto == null )
                return new DataTypeDictionary();
            return new DataTypeDictionary( dto.Id.ToGuid() ) {
                UserId = dto.UserId,
                Code = dto.Code,
                Name = dto.Name,
                Template = dto.Template,
                    IsDeleted = dto.IsDeleted.SafeValue(),
                Version = dto.Version,
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
                    Enabled = dto.Enabled.SafeValue(),
                SortId = dto.SortId,
                Note = dto.Note,
                Extend = dto.Extend,
            };
        }

        /// <summary>
        /// 转换为数据类型字典数据传输对象
        /// </summary>
        /// <param name="entity">数据类型字典实体</param>
        public static DataTypeDictionaryDto ToDto( this DataTypeDictionary entity ) {
            if( entity == null )
                return new DataTypeDictionaryDto();
            return new DataTypeDictionaryDto {
                Id = entity.Id.ToString(),
                UserId = entity.UserId,
                Code = entity.Code,
                Name = entity.Name,
                Template = entity.Template,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
                CreationTime = entity.CreationTime,
                CreatorId = entity.CreatorId,
                LastModificationTime = entity.LastModificationTime,
                LastModifierId = entity.LastModifierId,
                Enabled = entity.Enabled,
                SortId = entity.SortId,
                Note = entity.Note,
                Extend = entity.Extend,
            };
        }
    }
}