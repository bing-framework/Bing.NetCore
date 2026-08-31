using System.ComponentModel.DataAnnotations;

namespace Bing.Validation.Strategies;

/// <summary>
/// 定义可命名的对象验证策略契约。
/// </summary>
public interface IValidationStrategy
{
    /// <summary>
    /// 获取验证策略的名称。
    /// </summary>
    string StrategyName { get; }
}

/// <summary>
/// 定义针对指定对象类型执行验证的策略契约。
/// </summary>
/// <typeparam name="TObject">待验证的对象类型。</typeparam>
public interface IValidationStrategy<in TObject> : IValidationStrategy where TObject : class, IVerifyModel
{
    /// <summary>
    /// 验证指定对象并返回验证结果；验证通过时返回空结果。
    /// </summary>
    /// <param name="obj">待验证的对象。</param>
    /// <returns>验证失败时返回验证错误信息，否则返回 <see langword="null"/>。</returns>
    ValidationResult Validate(TObject obj);
}
