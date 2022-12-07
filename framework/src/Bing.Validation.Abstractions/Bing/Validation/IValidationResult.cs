using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bing.Validation;

/// <summary>
/// 验证结果集合
/// </summary>
public interface IValidationResult : IEnumerable<ValidationResult>
{
    /// <summary>
    /// 验证结果计数
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 是否已验证
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// 错误码
    /// </summary>
    long ErrorCode { get; set; }

    /// <summary>
    /// 标识
    /// </summary>
    string Flag { get; set; }

    /// <summary>
    /// 添加
    /// </summary>
    /// <param name="result">验证结果</param>
    void Add(ValidationResult result);

    /// <summary>
    /// 添加集合
    /// </summary>
    /// <param name="results">验证结果集合</param>
    void AddRange(IEnumerable<ValidationResult> results);

    /// <summary>
    /// 转换为消息
    /// </summary>
    string ToMessage();

    /// <summary>
    /// 转换为验证消息集合
    /// </summary>
    IEnumerable<string> ToValidationMessages();
}
