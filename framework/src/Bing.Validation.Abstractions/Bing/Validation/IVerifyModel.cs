using System.Collections.Generic;
using Bing.Validation.Strategies;

namespace Bing.Validation
{
    /// <summary>
    /// 可验证模型
    /// </summary>
    public interface IVerifyModel
    {
    }

    /// <summary>
    /// 可验证模型
    /// </summary>
    /// <typeparam name="TObject">对象类型</typeparam>
    public interface IVerifyModel<out TObject> : IVerifyModel where TObject : class, IVerifyModel
    {
        /// <summary>
        /// 设置验证处理器
        /// </summary>
        /// <param name="handler">验证处理器</param>
        void SetValidationCallback(IValidationCallbackHandler handler);

        /// <summary>
        /// 使用全局验证规则
        /// </summary>
        void UseValidationRules();

        /// <summary>
        /// 使用验证策略
        /// </summary>
        /// <param name="strategy">验证策略</param>
        void UseStrategy(IValidationStrategy<TObject> strategy);

        /// <summary>
        /// 使用验证策略集合
        /// </summary>
        /// <param name="strategies">验证策略集合</param>
        void UseStrategyList(IEnumerable<IValidationStrategy<TObject>> strategies);

        /// <summary>
        /// 验证
        /// </summary>
        IValidationResult Validate();
    }
}
