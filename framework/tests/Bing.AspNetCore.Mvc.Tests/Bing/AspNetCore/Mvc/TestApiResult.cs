using System;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 测试API结果
/// </summary>
public class TestApiResult
{
    /// <summary>
    /// 状态码
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// 数据
    /// </summary>
    public dynamic Data { get; set; }

    /// <summary>
    /// 操作时间
    /// </summary>
    public DateTime OperationTime { get; set; }
}
