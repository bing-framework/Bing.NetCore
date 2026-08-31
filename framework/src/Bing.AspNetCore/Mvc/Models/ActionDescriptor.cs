using System.ComponentModel;
using System.Reflection;
using Newtonsoft.Json;

namespace Bing.AspNetCore.Mvc.Models;

/// <summary>
/// 表示控制器操作的方法名称、描述及反射信息。
/// </summary>
public class ActionDescriptor
{
    /// <summary>
    /// 获取当前操作所属的控制器描述。
    /// </summary>
    public ControllerDescriptor Controller { get; protected set; }

    /// <summary>
    /// 获取操作方法名称。
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    /// 获取操作的显示描述；未配置 <see cref="DescriptionAttribute"/> 时为空。
    /// </summary>
    public string Description { get; protected set; }

    /// <summary>
    /// 获取操作方法的反射信息；该成员不会参与 JSON 序列化。
    /// </summary>
    [JsonIgnore]
    public MethodInfo MethodInfo { get; protected set; }

    /// <summary>
    /// 使用控制器描述和方法反射信息初始化 <see cref="ActionDescriptor"/> 的实例。
    /// </summary>
    /// <param name="controller">操作所属的控制器描述。</param>
    /// <param name="methodInfo">操作方法的反射信息。</param>
    public ActionDescriptor(ControllerDescriptor controller, MethodInfo methodInfo)
    {
        Controller = controller;
        MethodInfo = methodInfo;
        Init();
    }

    /// <summary>
    /// 从方法反射信息初始化操作名称和描述。
    /// </summary>
    private void Init()
    {
        Name = MethodInfo.Name;
        InitDescription();
    }

    /// <summary>
    /// 读取方法上的 <see cref="DescriptionAttribute"/> 并初始化操作描述。
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