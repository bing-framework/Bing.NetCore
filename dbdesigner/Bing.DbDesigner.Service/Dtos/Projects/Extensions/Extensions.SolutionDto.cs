using Bing;
using Bing.Extensions.AutoMapper;
using Bing.DbDesigner.Projects.Domain.Models;
using Bing.Utils.Extensions;

namespace Bing.DbDesigner.Service.Dtos.Projects.Extensions {
    /// <summary>
    /// 解决方案数据传输对象扩展
    /// </summary>
    public static class SolutionDtoExtension {

        /// <summary>
        /// 转换为解决方案实体
        /// </summary>
        /// <param name="dto">解决方案数据传输对象</param>
        public static Solution ToEntity2( this SolutionDto dto ) {
            if( dto == null )
                return new Solution();
            return new Solution( dto.Id.ToGuid() ) {
                UserId = dto.UserId,
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                    Enabled = dto.Enabled.SafeValue(),
                Note = dto.Note,
                SortId = dto.SortId,
                PinYin = dto.PinYin,
                CreationTime = dto.CreationTime,
                CreatorId = dto.CreatorId,
                LastModificationTime = dto.LastModificationTime,
                LastModifierId = dto.LastModifierId,
                    IsDeleted = dto.IsDeleted.SafeValue(),
                Version = dto.Version,
            };
        }
    
        /// <summary>
        /// 转换为解决方案数据传输对象
        /// </summary>
        /// <param name="entity">解决方案实体</param>
        public static SolutionDto ToDto( this Solution entity ) {
            if( entity == null )
                return new SolutionDto();
            return new SolutionDto {
                Id = entity.Id.ToString(),
                UserId = entity.UserId,
                Code = entity.Code,
                Name = entity.Name,
                Description = entity.Description,
                Enabled = entity.Enabled,
                Note = entity.Note,
                SortId = entity.SortId,
                PinYin = entity.PinYin,
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