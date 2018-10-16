using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Databases.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Databases.Extensions {
    /// <summary>
    /// 数据列数据传输对象扩展
    /// </summary>
    public static class DatabaseColumnDtoExtension {

        /// <summary>
        /// 转换为数据列实体
        /// </summary>
        /// <param name="dto">数据列数据传输对象</param>
        public static DatabaseColumn ToEntity( this DatabaseColumnDto dto ) {
            if( dto == null )
                return new DatabaseColumn();
            return new DatabaseColumn( dto.Id.ToGuid() ) {
                DatabaseId = dto.DatabaseId,
                DatabaseTableId = dto.DatabaseTableId,
                UserId = dto.UserId,
                Code = dto.Code,
                Name = dto.Name,
                Comment = dto.Comment,
                DataTypeCode = dto.DataTypeCode,
                DataTyepShow = dto.DataTyepShow,
                Length = dto.Length,
                DecimalPlaces = dto.DecimalPlaces,
                    IsPrimaryKey = dto.IsPrimaryKey.SafeValue(),
                    IsNull = dto.IsNull.SafeValue(),
                    IsForeignKey = dto.IsForeignKey.SafeValue(),
                    IsDeleted = dto.IsDeleted.SafeValue(),
                Version = dto.Version,
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
                SortId = dto.SortId,
                Note = dto.Note,
                Extend = dto.Extend,
            };
        }

        /// <summary>
        /// 转换为数据列数据传输对象
        /// </summary>
        /// <param name="entity">数据列实体</param>
        public static DatabaseColumnDto ToDto( this DatabaseColumn entity ) {
            if( entity == null )
                return new DatabaseColumnDto();
            return new DatabaseColumnDto {
                Id = entity.Id.ToString(),
                DatabaseId = entity.DatabaseId,
                DatabaseTableId = entity.DatabaseTableId,
                UserId = entity.UserId,
                Code = entity.Code,
                Name = entity.Name,
                Comment = entity.Comment,
                DataTypeCode = entity.DataTypeCode,
                DataTyepShow = entity.DataTyepShow,
                Length = entity.Length,
                DecimalPlaces = entity.DecimalPlaces,
                IsPrimaryKey = entity.IsPrimaryKey,
                IsNull = entity.IsNull,
                IsForeignKey = entity.IsForeignKey,
                IsDeleted = entity.IsDeleted,
                Version = entity.Version,
                CreationTime = entity.CreationTime,
                CreatorId = entity.CreatorId,
                LastModificationTime = entity.LastModificationTime,
                LastModifierId = entity.LastModifierId,
                SortId = entity.SortId,
                Note = entity.Note,
                Extend = entity.Extend,
            };
        }
    }
}