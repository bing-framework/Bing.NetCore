using Bing.AspNetCore.Mvc.Models;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// Api接口服务
/// </summary>
public interface IApiInterfaceService
{
    /// <summary>
    /// 获取所有控制器。不包含抽象的类
    /// </summary>
    /// <returns>应用程序中所有非抽象控制器的描述信息。</returns>
    IEnumerable<ControllerDescriptor> GetAllController();

    /// <summary>
    /// 获取所有操作
    /// </summary>
    /// <returns>应用程序中所有带 HTTP 方法特性的控制器操作描述信息。</returns>
    IEnumerable<ActionDescriptor> GetAllAction();
}