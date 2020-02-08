using System.Collections.Generic;

namespace Bing.Validations.Abstractions
{
    /// <summary>
    /// 验证
    /// </summary>
    public interface IValidatable
    {
        /// <summary>
        /// 验证
        /// </summary>
        ValidationResultCollection Validate();
    }

    /// <summary>
    /// 验证
    /// </summary>
    /// <typeparam name="TObject">对象类型</typeparam>
    public interface IValidatable<out TObject> : IValidatable where TObject : class, IValidatable
    {
        /// <summary>
        /// 设置验证处理器
        /// </summary>
        /// <param name="handler">验证处理器</param>
        void SetValidateHandler(IValidationHandler handler);

        /// <summary>
        /// 添加验证策略
        /// </summary>
        /// <param name="strategy">验证策略</param>
        void AddStrategy(IValidateStrategy<TObject> strategy);

        /// <summary>
        /// 添加验证策略集合
        /// </summary>
        /// <param name="strategies">验证策略集合</param>
        void AddStrategyList(IEnumerable<IValidateStrategy<TObject>> strategies);
    }
}
