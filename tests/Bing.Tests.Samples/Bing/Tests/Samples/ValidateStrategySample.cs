using System.ComponentModel.DataAnnotations;
using Bing.Validations.Abstractions;

namespace Bing.Tests.Samples
{
    /// <summary>
    /// 验证策略样例 - 名称长度大于3将验证失败
    /// </summary>
    public class ValidateStrategySample : IValidateStrategy<AggregateRootSample>
    {
        /// <summary>
        /// 策略名称
        /// </summary>
        public string StrategyName => nameof(ValidateStrategySample);

        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="obj">对象</param>
        public ValidationResult Validate(AggregateRootSample obj)
        {
            if (obj.Name.Length > 3)
                return new ValidationResult("名称长度不能大于3");
            return ValidationResult.Success;
        }
    }
}
