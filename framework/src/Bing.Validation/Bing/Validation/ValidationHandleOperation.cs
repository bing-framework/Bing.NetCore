using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bing.Validation
{
    /// <summary>
    /// 验证处理操作
    /// </summary>
    public sealed class ValidationHandleOperation
    {
        /// <summary>
        /// 验证结果集合
        /// </summary>
        private readonly ValidationResultCollection _collection;

        /// <summary>
        /// 初始化一个<see cref="ValidationHandleOperation"/>
        /// </summary>
        /// <param name="collection">验证结果集合</param>
        public ValidationHandleOperation(ValidationResultCollection collection) => _collection = collection ?? throw new ArgumentNullException(nameof(collection));

        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="handler">验证处理器</param>
        /// <param name="filterFunc">过滤函数</param>
        internal void Handle(IValidationCallbackHandler handler, Action<IEnumerable<ValidationResult>> filterFunc = null)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            var coll = filterFunc == null ? _collection : _collection.Filter(filterFunc);
            if (coll == null)
                return;
            handler.Handle(coll);
        }

        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="handler">验证处理器</param>
        /// <param name="strategyName">策略名称</param>
        internal void Handle(IValidationCallbackHandler handler, string strategyName)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            var coll = _collection.Filter(strategyName);
            if (coll == null)
                return;
            handler.Handle(coll);
        }

        /// <summary>
        /// 引发异常
        /// </summary>
        /// <typeparam name="TException">异常类型</typeparam>
        /// <param name="appendAction">追加操作</param>
        public void RaiseException<TException>(Action<TException, ValidationResultCollection> appendAction = null)
            where TException : BingException, new() =>
            _collection.RaiseException(appendAction);
    }
}
