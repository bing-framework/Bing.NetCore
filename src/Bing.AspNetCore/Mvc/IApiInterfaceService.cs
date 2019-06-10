using System.Collections.Generic;
using Bing.AspNetCore.Mvc.Models;

namespace Bing.AspNetCore.Mvc
{
    /// <summary>
    /// Api接口服务
    /// </summary>
    public interface IApiInterfaceService
    {
        /// <summary>
        /// 获取所有控制器。不包含抽象的类
        /// </summary>
        IEnumerable<ControllerDescriptor> GetAllController();

        /// <summary>
        /// 获取所有操作
        /// </summary>
        IEnumerable<ActionDescriptor> GetAllAction();
    }
}
