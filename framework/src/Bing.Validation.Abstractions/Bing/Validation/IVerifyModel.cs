using Bing.Validation.Strategies;

namespace Bing.Validation;

/// <summary>
/// 定义可执行验证的模型。
/// </summary>
public interface IVerifyModel
{
    /// <summary>
    /// 执行模型验证。
    /// </summary>
    /// <returns>包含本次验证结果的集合。</returns>
    IValidationResult Validate();
}

/// <summary>
/// 定义可配置验证策略的模型。
/// </summary>
/// <typeparam name="TObject">由验证策略处理的模型类型。</typeparam>
public interface IVerifyModel<out TObject> : IVerifyModel 
    where TObject : class, IVerifyModel
{
    /// <summary>
    /// 设置验证完成后的结果处理器。
    /// </summary>
    /// <param name="handler">用于处理验证结果的回调处理器。</param>
    void SetValidationCallback(IValidationCallbackHandler handler);

    /// <summary>
    /// 配置模型使用全局注册的验证规则。
    /// </summary>
    void UseValidationRules();

    /// <summary>
    /// 添加单个验证策略。
    /// </summary>
    /// <param name="strategy">要添加的验证策略。</param>
    void UseStrategy(IValidationStrategy<TObject> strategy);

    /// <summary>
    /// 添加多个验证策略。
    /// </summary>
    /// <param name="strategies">要添加的验证策略集合。</param>
    void UseStrategyList(IEnumerable<IValidationStrategy<TObject>> strategies);
}
