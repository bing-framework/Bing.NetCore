﻿using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 验证失败结果
/// </summary>
public class ValidationFailedResult : ObjectResult
{
    /// <summary>
    /// 允许多个结果
    /// </summary>
    public bool AllowMultipleResult { get; set; }

    /// <summary>
    /// 错误列表
    /// </summary>
    public List<ValidationError> Errors { get; }

    /// <summary>
    /// 初始化一个<see cref="ValidationFailedResult"/>类型的实例
    /// </summary>
    /// <param name="errors">验证错误列表</param>
    public ValidationFailedResult(List<ValidationError> errors) : base(errors)
    {
        StatusCode = StatusCodes.Status422UnprocessableEntity;
        Errors = errors;
    }
}

/// <summary>
/// 验证错误
/// </summary>
[DataContract]
public class ValidationError
{
    /// <summary>
    /// 名称
    /// </summary>
    [DataMember(EmitDefaultValue = false)]
    public string Name { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    [DataMember]
    public string Message { get; set; }
}