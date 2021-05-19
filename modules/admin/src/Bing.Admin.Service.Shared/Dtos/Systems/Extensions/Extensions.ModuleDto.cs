using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Data.Pos.Systems.Models;
using Bing.ObjectMapping;
using Bing.Utils.Json;

namespace Bing.Admin.Service.Shared.Dtos.Systems.Extensions
{
    /// <summary>
    /// 模块资源数据传输对象扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 转换为模块数据传输对象
        /// </summary>
        /// <param name="po">资源持久化实体</param>
        public static ModuleDto ToModuleDto(this ResourcePo po)
        {
            if (po == null)
                return null;
            var result = po.MapTo<ModuleDto>();
            result.Url = po.Uri;
            var extend = JsonHelper.ToObject<ModuleExtend>(po.Extend);
            extend.MapTo(result);
            return result;
        }
    }
}
