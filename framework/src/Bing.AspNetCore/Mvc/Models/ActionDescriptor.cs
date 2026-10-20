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
    /// 获取或设置当前操作所属的控制器描述。
    /// </summary>
    public ControllerDescriptor Controller { get; protected set; }

    /// <summary>
    /// 获取或设置操作方法名称。
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    /// 获取或设置操作的显示描述。
    /// </summary>
    /// <remarks>
    /// 未配置 时为空。
    /// </remarks>
    public string Description { get; protected set; }

    /// <summary>
    /// 获取或设置操作方法的反射信息。
    /// </summary>
    /// <remarks>
    /// 该成员不会参与 JSON 序列化。
    /// </remarks>
    [JsonIgnore]
    public MethodInfo MethodInfo { get; protected set; }

    /// <summary>
    /// 初始化一个 <see cref="ActionDescriptor"/> 类型的实例。
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