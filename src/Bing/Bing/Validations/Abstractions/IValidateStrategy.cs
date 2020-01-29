using System.ComponentModel.DataAnnotations;

namespace Bing.Validations.Abstractions
{
    /// <summary>
    /// 验证策略
    /// </summary>
    public interface IValidateStrategy
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
    public interface IValidateStrategy<in TObject> : IValidateStrategy where TObject : class, IValidatable
    {
        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="obj">对象</param>
        ValidationResult Validate(TObject obj);
    }
}
