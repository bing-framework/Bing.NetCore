using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 表示统一 API 响应的业务状态、消息、数据和操作时间。
/// </summary>
public class ApiResult : JsonResult
{
    /// <summary>
    /// 使用整数业务状态码初始化 <see cref="ApiResult"/> 的实例。
    /// </summary>
    /// <param name="code">业务状态码。</param>
    /// <param name="message">面向调用方的响应消息。</param>
    /// <param name="data">可选的响应数据。</param>
    /// <param name="httpStatusCode">可选的 HTTP 状态码。</param>
    public ApiResult(int code, string message, object data = null, int? httpStatusCode = null) 
        : base(null)
    {
        Code = code;
        Message = message;
        Data = data;
        OperationTime = DateTime.Now;
        StatusCode = httpStatusCode;
    }

    /// <summary>
    /// 使用框架业务状态枚举初始化 <see cref="ApiResult"/> 的实例。
    /// </summary>
    /// <param name="code">框架业务状态码。</param>
    /// <param name="message">面向调用方的响应消息。</param>
    /// <param name="data">可选的响应数据。</param>
    /// <param name="httpStatusCode">可选的 HTTP 状态码。</param>
    public ApiResult(StatusCode code, string message, object data = null, int? httpStatusCode = null) 
        : this((int)code, message, data, httpStatusCode)
    {
    }

    /// <summary>
    /// 获取业务状态码。
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// 获取响应消息。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 获取响应数据；无数据时为 <c>null</c>。
    /// </summary>
    public object Data { get; }

    /// <summary>
    /// 获取创建响应时记录的操作时间。
    /// </summary>
    public DateTime OperationTime { get; }

    /// <summary>
    /// 将统一响应对象写入 ASP.NET Core 执行上下文并委托基类完成 JSON 响应。
    /// </summary>
    /// <param name="context">当前 MVC 操作上下文。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 <c>null</c> 时抛出。</exception>
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        Value = new { Code = Code, Message = Message, OperationTime = OperationTime, Data = Data };
        return base.ExecuteResultAsync(context);
    }

    /// <summary>
    /// 创建业务状态为成功的 API 结果。
    /// </summary>
    /// <param name="message">响应消息，默认为“操作成功”。</param>
    /// <param name="data">可选的响应数据。</param>
    /// <returns>业务状态为成功的 API 结果。</returns>
    public static ApiResult Success(string message = "操作成功", object data = null)
    {
        return new ApiResult(Bing.AspNetCore.Mvc.StatusCode.Ok, message, data);
    }

    /// <summary>
    /// 创建业务状态为失败的 API 结果。
    /// </summary>
    /// <param name="message">响应消息，默认为“操作失败”。</param>
    /// <param name="data">可选的响应数据。</param>
    /// <returns>业务状态为失败的 API 结果。</returns>
    public static ApiResult Fail(string message = "操作失败", object data = null)
    {
        return new ApiResult(Bing.AspNetCore.Mvc.StatusCode.Fail, message, data);
    }
}
