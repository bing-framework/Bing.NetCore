using System;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 测试API结果
/// </summary>
public class TestApiResult
{
    /// <summary>
    /// 获取或设置状态码
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// 获取或设置消息
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// 获取或设置数据
    /// </summary>
    public dynamic Data { get; set; }

    /// <summary>
    /// 获取或设置操作时间
    /// </summary>
    public DateTime OperationTime { get; set; }
}
