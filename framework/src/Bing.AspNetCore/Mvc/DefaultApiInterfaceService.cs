using System.Reflection;
using Bing.AspNetCore.Mvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 默认Api接口服务
/// </summary>
internal class DefaultApiInterfaceService : IApiInterfaceService
{
    /// <summary>
    /// 保存已解析的 MVC 控制器描述列表。
    /// </summary>
    private List<ControllerDescriptor> _controllerDescriptors;

    /// <summary>
    /// 保存已解析的 MVC 操作描述列表。
    /// </summary>
    private List<ActionDescriptor> _actionDescriptors;

    /// <summary>
    /// 保存用于枚举 MVC 应用部件的应用程序部分管理器。
    /// </summary>
    private readonly ApplicationPartManager _partManager;

    /// <summary>
    /// 同步控制器和操作描述缓存的并发刷新。
    /// </summary>
    private static readonly object Lock = new object();

    /// <summary>
    /// 初始化一个 <see cref="DefaultApiInterfaceService"/> 类型的实例。
    /// </summary>
    /// <param name="partManager">应用程序部分管理</param>
    public DefaultApiInterfaceService(ApplicationPartManager partManager)
    {
        _partManager = partManager;
    }

    /// <inheritdoc />
    public IEnumerable<ControllerDescriptor> GetAllController()
    {
        lock (Lock)
        {
            if (_controllerDescriptors != null && _controllerDescriptors.Any())
            {
                return _controllerDescriptors;
            }

            _controllerDescriptors = new List<ControllerDescriptor>();

            var controllerFeature = new ControllerFeature();
            _partManager.PopulateFeature(controllerFeature);
            var controllerTypes = controllerFeature.Controllers;
            foreach (var typeInfo in controllerTypes)
            {
                if (typeInfo.IsAbstract)
                {
                    continue;
                }

                var controller = new ControllerDescriptor(typeInfo);
                _controllerDescriptors.Add(controller);
            }
        }
        return _controllerDescriptors;
    }

    /// <inheritdoc />
    public IEnumerable<ActionDescriptor> GetAllAction()
    {
        lock (Lock)
        {
            if (_actionDescriptors != null && _actionDescriptors.Any())
            {
                return _actionDescriptors;
            }

            _actionDescriptors = new List<ActionDescriptor>();

            var controllers = GetAllController();
            foreach (var controller in controllers)
            {
                foreach (var method in controller.TypeInfo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (!method.CustomAttributes.Any(m =>
                            m.AttributeType == typeof(HttpGetAttribute)
                            || m.AttributeType == typeof(HttpPostAttribute)
                            || m.AttributeType == typeof(HttpPutAttribute)
                            || m.AttributeType == typeof(HttpOptionsAttribute)
                            || m.AttributeType == typeof(HttpHeadAttribute)
                            || m.AttributeType == typeof(HttpPatchAttribute)
                            || m.AttributeType == typeof(HttpDeleteAttribute)))
                    {
                        continue;
                    }

                    var action = new ActionDescriptor(controller, method);
                    _actionDescriptors.Add(action);
                }
            }
        }

        return _actionDescriptors;
    }
}