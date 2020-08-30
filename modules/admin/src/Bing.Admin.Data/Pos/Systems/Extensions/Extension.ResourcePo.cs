using Bing.Admin.Data.Pos.Systems.Models;
using Bing.Admin.Domain.Shared.Enums;
using Bing.Admin.Systems.Domain.Models;
using Bing.Extensions;
using Bing.Mapping;
using Bing.Utils.Json;

namespace Bing.Admin.Data.Pos.Systems.Extensions
{
    /// <summary>
    /// 资源持久化对象扩展
    /// </summary>
    public static partial class Extension
    {

        #region ToModule(转换为模块)

        /// <summary>
        /// 转换为模块
        /// </summary>
        /// <param name="po">资源持久化对象</param>
        public static Module ToModule(this ResourcePo po)
        {
            if (po == null)
                return null;
            if (po.Type != ResourceType.Module)
                return null;
            var result = po.MapTo(new Module(po.Id, po.Path, po.Level));
            result.Url = po.Uri;
            var extend = JsonHelper.ToObject<ModuleExtend>(po.Extend);
            extend.MapTo(result);
            return result;
        }

        #endregion

        #region ToOperation(转换为操作)

        /// <summary>
        /// 转换为操作
        /// </summary>
        /// <param name="po">资源持久化对象</param>
        public static Operation ToOperation(this ResourcePo po)
        {
            if (po == null)
                return null;
            if (po.Type != ResourceType.Operation)
                return null;
            var result = po.MapTo(new Operation(po.Id));
            result.ModuleId = po.ParentId.SafeValue();
            result.Code = po.Uri;
            var extend = JsonHelper.ToObject<OperationExtend>(po.Extend);
            extend.MapTo(result);
            return result;
        }

        #endregion

        #region ToPo(转换为资源持久化对象)

        /// <summary>
        /// 转换为资源持久化对象
        /// </summary>
        /// <param name="entity">模块</param>
        public static ResourcePo ToPo(this Module entity)
        {
            if (entity == null)
                return null;
            var result = entity.MapTo<ResourcePo>();
            result.Type = ResourceType.Module;
            result.Uri = entity.Url;
            result.Extend = JsonHelper.ToJson(CreateExtend(entity));
            return result;
        }

        /// <summary>
        /// 创建模块扩展对象
        /// </summary>
        /// <param name="entity">模块</param>
        private static ModuleExtend CreateExtend(Module entity)
        {
            return new ModuleExtend()
            {
                Icon = entity.Icon,
                Expanded = entity.Expanded
            };
        }

        /// <summary>
        /// 转换为资源持久化对象
        /// </summary>
        /// <param name="entity">操作</param>
        public static ResourcePo ToPo(this Operation entity)
        {
            if (entity == null)
                return null;
            var result = entity.MapTo<ResourcePo>();
            result.ParentId = entity.ModuleId;
            result.Type = ResourceType.Operation;
            result.Uri = entity.Code;
            result.Path = $"{entity.Id},";
            result.Level = 1;
            result.Extend = JsonHelper.ToJson(CreateExtend(entity));
            return result;
        }

        /// <summary>
        /// 创建操作扩展对象
        /// </summary>
        /// <param name="entity">模块</param>
        private static OperationExtend CreateExtend(Operation entity)
        {
            return new OperationExtend()
            {
                Icon = entity.Icon
            };
        }

        #endregion

    }
}
