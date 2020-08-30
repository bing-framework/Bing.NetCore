using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Data.Pos.Systems.Models;
using Bing.Mapping;
using Bing.Utils.Json;

namespace Bing.Admin.Service.Shared.Dtos.Systems.Extensions
{
    /// <summary>
    /// 应用程序数据传输对象扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 转换为应用程序实体
        /// </summary>
        /// <param name="dto">应用程序数据传输对象</param>
        public static Admin.Systems.Domain.Models.Application ToEntity(this ApplicationDto dto)
        {
            var result = dto?.MapTo<Admin.Systems.Domain.Models.Application>();
            return result;
        }

        /// <summary>
        /// 转换为应用程序数据传输对象
        /// </summary>
        /// <param name="po">应用程序持久化对象</param>
        public static ApplicationDto ToDto(this ApplicationPo po)
        {
            if (po == null)
                return null;
            var result = po.MapTo<ApplicationDto>();
            var extend = JsonHelper.ToObject<ApplicationExtend>(po.Extend);
            if (extend == null)
                return result;
            extend.MapTo(result);
            return result;
        }
    }
}
