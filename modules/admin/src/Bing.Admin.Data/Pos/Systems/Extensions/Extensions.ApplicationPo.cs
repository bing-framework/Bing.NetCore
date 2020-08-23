using Bing.Admin.Data.Pos.Systems.Models;
using Bing.Admin.Systems.Domain.Models;
using Bing.Mapping;
using Bing.Utils.Json;

namespace Bing.Admin.Data.Pos.Systems.Extensions
{
    /// <summary>
    /// 应用程序持久化对象扩展
    /// </summary>
    public static class ApplicationPoExtensions
    {
        /// <summary>
        /// 转换为实体
        /// </summary>
        /// <param name="po">持久化对象</param>
        public static Admin.Systems.Domain.Models.Application ToEntity(this ApplicationPo po)
        {
            if (po == null)
                return null;
            var result = po.MapTo(new Admin.Systems.Domain.Models.Application(po.Id));
            var extend = JsonHelper.ToObject<ApplicationExtend>(po.Extend);
            if (extend == null)
                return result;
            extend.MapTo(result);
            return result;
        }

        /// <summary>
        /// 转换为持久化对象
        /// </summary>
        /// <param name="entity">实体</param>
        public static ApplicationPo ToPo(this Admin.Systems.Domain.Models.Application entity)
        {
            if (entity == null)
                return null;
            var result = entity.MapTo<ApplicationPo>();
            result.Extend = JsonHelper.ToJson(CreateExtend(entity));
            return result;
        }

        /// <summary>
        /// 创建扩展
        /// </summary>
        /// <param name="entity">应用程序</param>
        private static ApplicationExtend CreateExtend(Admin.Systems.Domain.Models.Application entity) => entity.MapTo<ApplicationExtend>();
    }
}
