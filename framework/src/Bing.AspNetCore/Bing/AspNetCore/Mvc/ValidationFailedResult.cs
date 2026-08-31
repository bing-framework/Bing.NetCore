using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 表示模型验证失败时返回的 HTTP 422 结果。
/// </summary>
public class ValidationFailedResult : ObjectResult
{
    /// <summary>
    /// 获取或设置是否允许处理多个验证结果。
    /// </summary>
    public bool AllowMultipleResult { get; set; }

    /// <summary>
    /// 获取验证错误明细列表。
    /// </summary>
    public List<ValidationError> Errors { get; }

    /// <summary>
    /// 使用验证错误列表初始化 <see cref="ValidationFailedResult"/> 的实例。
    /// </summary>
    /// <param name="errors">要写入响应正文的验证错误列表。</param>
    public ValidationFailedResult(List<ValidationError> errors) : base(errors)
    {
        StatusCode = StatusCodes.Status422UnprocessableEntity;
        Errors = errors;
    }
}

/// <summary>
/// 表示单项模型验证错误及其关联成员。
/// </summary>
[DataContract]
public class ValidationError
{
    /// <summary>
    /// 获取或设置触发验证错误的参数、字段或属性名称；未关联具体成员时不输出默认值。
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置面向调用方的验证错误消息。
    /// </summary>
    [DataMember]
    public string Message { get; set; }
}
