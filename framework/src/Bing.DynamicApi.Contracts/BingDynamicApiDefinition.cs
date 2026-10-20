using System.Collections.Generic;

namespace Bing.DynamicApi.Contracts;

/// <summary>
/// 描述某一 API 版本中可供远程调用的动态服务。
/// </summary>
public class BingDynamicApiDefinition
{
    /// <summary>
    /// 获取或设置描述结构版本。
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// 获取或设置服务端 API 版本。
    /// </summary>
    public string ApiVersion { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置当前版本的服务描述集合。
    /// </summary>
    public List<BingDynamicApiServiceDefinition> Services { get; set; } = new();
}

/// <summary>
/// 描述一个应用服务及其远程方法。
/// </summary>
public class BingDynamicApiServiceDefinition
{
    /// <summary>
    /// 获取或设置服务名称。
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置共享服务接口的完整类型名。
    /// </summary>
    public string ServiceTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置服务的方法描述集合。
    /// </summary>
    public List<BingDynamicApiMethodDefinition> Methods { get; set; } = new();
}

/// <summary>
/// 描述一个动态 API 方法。
/// </summary>
public class BingDynamicApiMethodDefinition
{
    /// <summary>
    /// 获取或设置方法名称。
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 HTTP 动词。
    /// </summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置相对路由模板。
    /// </summary>
    /// <remarks>模板相对于应用根地址。</remarks>
    public string RouteTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置返回类型名称。
    /// </summary>
    /// <remarks>使用完整类型表示，不包含程序集版本信息。</remarks>
    public string ReturnTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置是否返回远程流内容。
    /// </summary>
    public bool IsStreamResult { get; set; }

    /// <summary>
    /// 获取或设置参数描述集合。
    /// </summary>
    public List<BingDynamicApiParameterDefinition> Parameters { get; set; } = new();
}

/// <summary>
/// 描述动态 API 方法的一个参数及其 HTTP 绑定来源。
/// </summary>
public class BingDynamicApiParameterDefinition
{
    /// <summary>
    /// 获取或设置参数名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置参数位置。
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// 获取或设置参数类型名称。
    /// </summary>
    /// <remarks>使用完整类型表示，不包含程序集版本信息。</remarks>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 MVC 绑定来源名称。
    /// </summary>
    public string BindingSource { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置参数是否可选。
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// 获取或设置参数是否为取消令牌。
    /// </summary>
    public bool IsCancellationToken { get; set; }

    /// <summary>
    /// 获取或设置参数是否为上传文件。
    /// </summary>
    public bool IsFile { get; set; }
}
