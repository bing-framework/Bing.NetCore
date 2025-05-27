using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// API 结果
/// </summary>
public class ApiResult : JsonResult
{
    /// <summary>
    /// 初始化一个<see cref="ApiResult"/>类型的实例
    /// </summary>
    /// <param name="code">错误码</param>
    /// <param name="message">消息</param>
    /// <param name="data">数据</param>
    /// <param name="httpStatusCode">Http状态码</param>
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
    /// 初始化一个<see cref="ApiResult"/>类型的实例
    /// </summary>
    /// <param name="code">错误码</param>
    /// <param name="message">消息</param>
    /// <param name="data">数据</param>
    /// <param name="httpStatusCode">Http状态码</param>
    public ApiResult(StatusCode code, string message, object data = null, int? httpStatusCode = null) 
        : this((int)code, message, data, httpStatusCode)
    {
    }

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
    public object Data { get; }

    /// <summary>
    /// 操作时间
    /// </summary>
    public DateTime OperationTime { get; }

    /// <summary>
    /// 执行结果
    /// </summary>
    /// <param name="context">操作上下文</param>
    /// <exception cref="ArgumentNullException">当<paramref name="context"/>为null时抛出</exception>
    public override Task ExecuteResultAsync(ActionContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        Value = new { Code = Code, Message = Message, OperationTime = OperationTime, Data = Data };
        return base.ExecuteResultAsync(context);
    }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="data">数据</param>
    /// <returns>API结果</returns>
    public static ApiResult Success(string message = "操作成功", object data = null)
    {
        return new ApiResult(Bing.AspNetCore.Mvc.StatusCode.Ok, message, data);
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="message">消息</param>
    /// <param name="data">数据</param>
    /// <returns>API结果</returns>
    public static ApiResult Fail(string message = "操作失败", object data = null)
    {
        return new ApiResult(Bing.AspNetCore.Mvc.StatusCode.Fail, message, data);
    }
}
