using Bing.Validation;

namespace Bing.Aop.AspectCore.Samples;

/// <summary>
/// 验证时直接抛出 InvalidOperationException 的测试模型，
/// 用于验证 <see cref="Bing.Validation.ValidAttribute"/> 的异常传播行为。
/// </summary>
public class ThrowingValidModel : IVerifyModel
{
    /// <summary>
    /// 始终抛出异常，模拟验证失败场景
    /// </summary>
    public IValidationResult Validate() =>
        throw new InvalidOperationException("ThrowingValidModel: validation failed by design");
}

/// <summary>
/// 记录 Validate() 是否被调用的测试模型，
/// 用于验证 <see cref="Bing.Validation.ValidAttribute"/> 是否正确触发验证。
/// </summary>
public class TrackingValidModel : IVerifyModel
{
    /// <summary>
    /// 是否已被调用过 Validate()
    /// </summary>
    public bool WasValidated { get; private set; }

    /// <summary>
    /// 标记已验证并返回 null（结果被 ValidAttribute 丢弃，null 安全）
    /// </summary>
    public IValidationResult Validate()
    {
        WasValidated = true;
        return null!;
    }
}
