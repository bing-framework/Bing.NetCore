using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Configurations;
using Bing.Validations.Abstractions;

namespace Bing.Validations
{
    /// <summary>
    /// 验证上下文
    /// </summary>
    /// <typeparam name="TObject">对象类型</typeparam>
    public class ValidationContext<TObject> where TObject : class, IValidatable
    {
        /// <summary>
        /// 对象实例
        /// </summary>
        private TObject Instance { get; set; }

        /// <summary>
        /// 验证策略列表
        /// </summary>
        private List<IValidateStrategy<TObject>> ValidateStrategyList { get; }

        /// <summary>
        /// 验证结果集合
        /// </summary>
        private ValidationResultCollection ResultCollection { get; set; }

        /// <summary>
        /// 处理操作
        /// </summary>
        private Action<ValidationHandleOperation> Handle { get; set; }

        /// <summary>
        /// 初始化一个<see cref="ValidationContext{TObject}"/>类型的实例
        /// </summary>
        /// <param name="instanceToValidate">验证实例</param>
        public ValidationContext(TObject instanceToValidate)
        {
            Instance = instanceToValidate;
            ValidateStrategyList = new List<IValidateStrategy<TObject>>();
        }

        /// <summary>
        /// 是否已验证
        /// </summary>
        public bool IsValid => ResultCollection?.IsValid ?? true;

        /// <summary>
        /// 添加验证策略
        /// </summary>
        /// <param name="strategy">验证策略</param>
        public void AddStrategy(IValidateStrategy<TObject> strategy)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));
            if (ValidateStrategyList.Any(x => x.StrategyName == strategy.StrategyName))
                return;
            ValidateStrategyList.Add(strategy);
        }

        /// <summary>
        /// 添加验证策略列表
        /// </summary>
        /// <param name="strategies">验证策略列表</param>
        public void AddStrategyList(IEnumerable<IValidateStrategy<TObject>> strategies)
        {
            if (strategies == null)
                throw new ArgumentNullException(nameof(strategies));
            foreach (var strategy in strategies)
                AddStrategy(strategy);
        }

        /// <summary>
        /// 设置验证处理器
        /// </summary>
        /// <param name="action">操作</param>
        public void SetHandler(Action<ValidationHandleOperation> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (Handle == null)
                Handle = action;
            else
                Handle += action;
        }

        /// <summary>
        /// 引发异常
        /// </summary>
        /// <typeparam name="TException">异常类型</typeparam>
        /// <param name="appendAction">追加操作</param>
        public void RaiseException<TException>(Action<TException, ValidationResultCollection> appendAction = null)
            where TException : BingException, new()
        {
            if (ResultCollection != null && !ResultCollection.IsValid)
                ResultCollection.RaiseException(appendAction);
        }

        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="appendAction">追加操作</param>
        public void Validate(Action<ValidationResultCollection> appendAction = null)
        {
            var result = DataAnnotationValidation.Validate(Instance);
            ResultCollection = new ValidationResultCollection(result);
            appendAction?.Invoke(ResultCollection);
            var tempList = ValidateStrategyList.Select(strategy => strategy.Validate(Instance)).ToList();
            if (tempList.Any())
                ResultCollection.AddRange(tempList);
            if (ResultCollection.IsValid)
                return;
            if (Handle == null)
                Handle = op => op.HandleAll(BingConfig.Current.ValidationHandler);// 如果没有处理器操作，则使用默认操作
            Handle.Invoke(ResultCollection.Handle());
        }

        /// <summary>
        /// 验证并引发异常
        /// </summary>
        /// <typeparam name="TException">异常类型</typeparam>
        /// <param name="appendAction">追加操作</param>
        public void ValidateAndRaise<TException>(Action<TException, ValidationResultCollection> appendAction = null)
            where TException : BingException, new()
        {
            Validate();
            RaiseException(appendAction);
        }

        /// <summary>
        /// 获取验证结果集合
        /// </summary>
        /// <returns></returns>
        public ValidationResultCollection GetValidationResultCollection() => ResultCollection;
    }
}
