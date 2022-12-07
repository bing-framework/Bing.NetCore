using System;
using System.ComponentModel;
using System.Reflection;
using Newtonsoft.Json;

namespace Bing.AspNetCore.Mvc.Models;

/// <summary>
/// 操作描述
/// </summary>
public class ActionDescriptor
{
    /// <summary>
    /// 控制器描述
    /// </summary>
    public ControllerDescriptor Controller { get; protected set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; protected set; }

    /// <summary>
    /// 方法信息
    /// </summary>
    [JsonIgnore]
    public MethodInfo MethodInfo { get; protected set; }

    /// <summary>
    /// 初始化一个<see cref="ActionDescriptor"/>类型的实例
    /// </summary>
    /// <param name="controller">控制器</param>
    /// <param name="methodInfo">方法信息</param>
    public ActionDescriptor(ControllerDescriptor controller, MethodInfo methodInfo)
    {
        Controller = controller;
        MethodInfo = methodInfo;
        Init();
    }

    /// <summary>
    /// 初始化
    /// </summary>
    private void Init()
    {
        Name = MethodInfo.Name;
        InitDescription();
    }

    /// <summary>
    /// 初始化描述
    /// </summary>
    protected virtual void InitDescription()
    {
        var attribute = Attribute.GetCustomAttribute(MethodInfo, typeof(DescriptionAttribute));
        if (attribute is DescriptionAttribute descriptionAttribute)
        {
            Description = descriptionAttribute.Description;
        }
    }
}