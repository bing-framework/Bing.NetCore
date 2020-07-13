using System.Collections.Generic;

namespace Bing.Validations.Abstractions
{
    /// <summary>
    /// 验证器
    /// </summary>
    public interface IValidator
    {
    }

    /// <summary>
    /// 验证器
    /// </summary>
    /// <typeparam name="TObject">对象类型</typeparam>
    public interface IValidator<out TObject> : IValidator where TObject : class, IValidatable
    {
        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="strategy">验证策略</param>
        void Validate(IValidateStrategy<TObject> strategy);

        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="strategies">验证策略集合</param>
        void Validate(IEnumerable<IValidateStrategy<TObject>> strategies);

        /// <summary>
        /// 获取验证结果集合
        /// </summary>
        ValidationResultCollection GetValidationResult();
    }
}
