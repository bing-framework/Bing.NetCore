using System;
using System.Reflection;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 配置一个动态 API 程序集的发现和路由约定。
/// </summary>
public class BingDynamicApiOptions
{
    /// <summary>
    /// 获取或设置动态 API 路由前缀。
    /// </summary>
    public string RoutePrefix { get; set; } = "api/app";

    /// <summary>
    /// 获取或设置动态 API 版本。
    /// </summary>
    /// <remarks>默认版本为 v1。</remarks>
    public string ApiVersion { get; set; } = "v1";

    /// <summary>
    /// 获取或设置是否在路由中包含版本。
    /// </summary>
    public bool IncludeVersionInRoute { get; set; } = true;

    /// <summary>
    /// 获取或设置服务类型筛选谓词。
    /// </summary>
    public Func<Type, bool> TypePredicate { get; set; }

    /// <summary>
    /// 获取或设置服务路由名称计算函数。
    /// </summary>
    public Func<Type, string> ServiceNameFactory { get; set; }

    /// <summary>
    /// 获取或设置方法路由计算函数。
    /// </summary>
    /// <remarks>返回相对于服务路由的模板。</remarks>
    public Func<MethodInfo, string> MethodRouteFactory { get; set; }

}
