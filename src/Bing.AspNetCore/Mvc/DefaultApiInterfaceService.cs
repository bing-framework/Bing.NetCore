using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bing.AspNetCore.Mvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Bing.AspNetCore.Mvc
{
    /// <summary>
    /// 默认Api接口服务
    /// </summary>
    internal class DefaultApiInterfaceService : IApiInterfaceService
    {
        /// <summary>
        /// 控制器描述列表
        /// </summary>
        private List<ControllerDescriptor> _controllerDescriptors;

        /// <summary>
        /// 操作描述列表
        /// </summary>
        private List<ActionDescriptor> _actionDescriptors;

        /// <summary>
        /// 应用程序部分管理
        /// </summary>
        private readonly ApplicationPartManager _partManager;

        /// <summary>
        /// 对象锁
        /// </summary>
        private static readonly object Lock = new object();

        /// <summary>
        /// 初始化一个<see cref="DefaultApiInterfaceService"/>类型的实例
        /// </summary>
        /// <param name="partManager">应用程序部分管理</param>
        public DefaultApiInterfaceService(ApplicationPartManager partManager)
        {
            _partManager = partManager;
        }

        /// <summary>
        /// 获取所有控制器。不包含抽象的类
        /// </summary>
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

        /// <summary>
        /// 获取所有操作
        /// </summary>
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
                    foreach (var method in controller.TypeInfo.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly))
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
}
