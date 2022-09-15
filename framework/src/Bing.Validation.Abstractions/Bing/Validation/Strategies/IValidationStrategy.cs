using System.ComponentModel.DataAnnotations;

namespace Bing.Validation.Strategies
{
    /// <summary>
    /// 验证策略
    /// </summary>
    public interface IValidationStrategy
    {
        /// <summary>
        /// 策略名称
        /// </summary>
        string StrategyName { get; }
    }

    /// <summary>
    /// 验证策略
    /// </summary>
    /// <typeparam name="TObject">对象类型</typeparam>
    public interface IValidationStrategy<in TObject> : IValidationStrategy where TObject : class, IVerifyModel
    {
        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="obj">对象</param>
        ValidationResult Validate(TObject obj);
    }
}
