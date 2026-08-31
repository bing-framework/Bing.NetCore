using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Authorization;

/// <summary>
/// 表示授权处理返回的 JSON 结果。
/// </summary>
public class AuthorizeResult : JsonResult
{
    /// <summary>
    /// 获取授权处理结果状态码。
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// 获取授权处理结果消息。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 获取授权处理结果携带的数据。
    /// </summary>
    public dynamic Data { get; }

    /// <summary>
    /// 获取结果创建时的操作时间。
    /// </summary>
    public DateTime OperationTime { get; }

    /// <summary>
    /// 使用状态码、消息和可选数据初始化一个 <see cref="AuthorizeResult"/> 实例。
    /// </summary>
    /// <param name="code">授权处理结果状态码。</param>
    /// <param name="message">授权处理结果消息。</param>
    /// <param name="data">授权处理结果携带的数据，可为空。</param>
    public AuthorizeResult(int code, string message, dynamic data = null) : base(null)
    {
        Code = code;
        Message = message;
        Data = data;
        OperationTime = DateTime.Now;
    }

    /// <summary>
    /// 将授权结果写入 MVC 执行上下文并异步执行 JSON 结果。
    /// </summary>
    /// <param name="context">当前 MVC 操作执行上下文。</param>
    /// <exception cref="ArgumentNullException">执行上下文为空时抛出。</exception>
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
