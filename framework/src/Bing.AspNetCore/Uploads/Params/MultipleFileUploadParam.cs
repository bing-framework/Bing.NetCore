using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.Uploads.Params;

/// <summary>
/// 封装多文件上传处理所需的请求、文件集合和目标目录信息。
/// </summary>
public class MultipleFileUploadParam
{
    /// <summary>
    /// 获取或设置承载上传文件的当前 HTTP 请求。
    /// </summary>
    public HttpRequest Request { get; set; }

    /// <summary>
    /// 获取或设置待保存的上传文件集合。
    /// </summary>
    public IList<IFormFile> FormFiles { get; set; }

    /// <summary>
    /// 获取或设置文件存储根路径。
    /// </summary>
    public string RootPath { get; set; }

    /// <summary>
    /// 获取或设置用于划分存储目录的模块名称。
    /// </summary>
    public string Module { get; set; }

    /// <summary>
    /// 获取或设置用于划分存储目录的分组名称。
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// 获取由根路径、模块和分组拼接得到的物理存储目录。
    /// </summary>
    public string FullPath => Path.Combine(RootPath, Module, Group);

    /// <summary>
    /// 获取由模块和分组拼接得到的相对存储目录。
    /// </summary>
    public string RelativePath => Path.Combine(Module, Group);
}