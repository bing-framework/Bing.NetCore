using System.ComponentModel.DataAnnotations;
using Bing.Validation.Strategies;

namespace Bing.Tests.Samples;

/// <summary>
/// 验证策略样例 - 名称长度大于3将验证失败
/// </summary>
public class ValidateStrategySample : IValidationStrategy<AggregateRootSample>
{
    /// <inheritdoc />
    public string StrategyName => nameof(ValidateStrategySample);

    /// <inheritdoc />
    public ValidationResult Validate(AggregateRootSample obj)
    {
        if (obj.Name.Length > 3)
            return new ValidationResult("名称长度不能大于3");
        return ValidationResult.Success;
    }
}
