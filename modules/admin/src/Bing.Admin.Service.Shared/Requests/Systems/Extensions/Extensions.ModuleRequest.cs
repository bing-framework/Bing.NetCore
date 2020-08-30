using Bing.Admin.Systems.Domain.Models;
using Bing.Mapping;

namespace Bing.Admin.Service.Shared.Requests.Systems.Extensions
{
    /// <summary>
    /// 模块资源数据传输对象扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 转换为模块实体
        /// </summary>
        /// <param name="request">请求</param>
        public static Module ToModule(this CreateModuleRequest request)
        {
            return request?.MapTo<Module>();
        }

        /// <summary>
        /// 转换为操作实体
        /// </summary>
        /// <param name="request">请求</param>
        public static Operation ToOperation(this CreateOperationRequest request)
        {
            return request?.MapTo<Operation>();
        }
    }
}
