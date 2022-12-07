using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Permissions.Authorization;

/// <summary>
/// 授权结果
/// </summary>
public class AuthorizeResult : JsonResult
{
    /// <summary>
    /// 状态码
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 数据
    /// </summary>
    public dynamic Data { get; }

    /// <summary>
    /// 操作时间
    /// </summary>
    public DateTime OperationTime { get; }

    /// <summary>
    /// 初始化一个<see cref="AuthorizeResult"/>类型的实例
    /// </summary>
    /// <param name="code">状态码</param>
    /// <param name="message">消息</param>
    /// <param name="data">数据</param>
    public AuthorizeResult(int code, string message, dynamic data = null) : base(null)
    {
        Code = code;
        Message = message;
        Data = data;
        OperationTime = DateTime.Now;
    }

    /// <summary>
    /// 执行结果
    /// </summary>
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        this.Value = new
        {
            Code = Code,
            Message = Message,
            OperationTime = OperationTime,
            Data = Data
        };
        return base.ExecuteResultAsync(context);
    }
}