using System.ComponentModel.DataAnnotations;

namespace Bing.Validation;

/// <summary>
/// 表示一次验证产生的错误集合及其业务错误信息。
/// </summary>
public interface IValidationResult : IEnumerable<ValidationResult>
{
    /// <summary>
    /// 获取当前集合中的验证结果数量。
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 获取验证是否通过；集合中不存在验证错误时返回 <c>true</c>。
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// 获取或设置验证结果集合对应的业务错误码。
    /// </summary>
    long ErrorCode { get; set; }

    /// <summary>
    /// 获取或设置用于标识验证结果来源或处理策略的标识。
    /// </summary>
    string Flag { get; set; }

    /// <summary>
    /// 将单个验证结果添加到当前集合。
    /// </summary>
    /// <param name="result">要添加的验证结果。</param>
    void Add(ValidationResult result);

    /// <summary>
    /// 将多个验证结果添加到当前集合。
    /// </summary>
    /// <param name="results">要添加的验证结果集合。</param>
    void AddRange(IEnumerable<ValidationResult> results);

    /// <summary>
    /// 将当前验证结果转换为汇总诊断消息。
    /// </summary>
    /// <returns>当前验证状态的汇总消息；没有错误时返回成功提示。</returns>
    string ToMessage();

    /// <summary>
    /// 将当前验证结果转换为逐项诊断消息。
    /// </summary>
    /// <returns>验证错误消息序列；没有错误时返回空序列。</returns>
    IEnumerable<string> ToValidationMessages();
}
