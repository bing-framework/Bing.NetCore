using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Bing.AspNetCore.Mvc.Models;

/// <summary>
/// 表示 MVC 控制器的区域、名称、描述和反射类型信息。
/// </summary>
public class ControllerDescriptor
{
    /// <summary>
    /// 获取或设置控制器所属的 ASP.NET Core 区域名称。
    /// </summary>
    public string Area { get; protected set; }

    /// <summary>
    /// 获取或设置移除 Controller 后缀的控制器名称。
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    /// 获取或设置控制器的显示描述。
    /// </summary>
    /// <remarks>
    /// 未配置 时为空。
    /// </remarks>
    public string Description { get; protected set; }

    /// <summary>
    /// 获取控制器类型的反射信息。
    /// </summary>
    /// <remarks>
    /// 该成员不会参与 JSON 序列化。
    /// </remarks>
    [JsonIgnore]
    public TypeInfo TypeInfo { get; }

    /// <summary>
    /// 初始化一个 <see cref="ControllerDescriptor"/> 类型的实例。
    /// </summary>
    /// <param name="typeInfo">控制器类型的反射信息。</param>
    public ControllerDescriptor(TypeInfo typeInfo)
    {
        TypeInfo = typeInfo;
        Init();
    }

    /// <summary>
    /// 初始化控制器名称、区域和描述。
    /// </summary>
    private void Init()
    {
        InitName();
        InitArea();
        InitDescription();
    }

    /// <summary>
    /// 根据控制器类型名称初始化显示名称。
    /// </summary>
    protected virtual void InitName()
    {
        Name = TypeInfo.Name.Replace("Controller", "");
    }

    /// <summary>
    /// 读取控制器上的 <see cref="AreaAttribute"/> 并初始化区域名称。
    /// </summary>
    protected virtual void InitArea()
    {
        var attribute = Attribute.GetCustomAttribute(TypeInfo, typeof(AreaAttribute));
        if (attribute is AreaAttribute areaAttribute)
        {
            Area = areaAttribute.RouteValue;
        }
    }

    /// <summary>
    /// 读取控制器上的 <see cref="DescriptionAttribute"/> 并初始化显示描述。
    /// </summary>
    protected virtual void InitDescription()
    {
        var attribute = Attribute.GetCustomAttribute(TypeInfo, typeof(DescriptionAttribute));
        if (attribute is DescriptionAttribute descriptionAttribute)
        {
            Description = descriptionAttribute.Description;
        }
    }
}