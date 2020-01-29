using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Bing.Exceptions;

namespace Bing.Validations
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

        internal void Handle(IValidationHandler handler, Action<IEnumerable<ValidationResult>> filterFunc = null)
        {

        }

        internal void Handle(IValidationHandler handler, string strategyName)
        {

        }

        public void RaiseException<TException>(Action<TException, ValidationResultCollection> appendAction = null)
            where TException : Warning, new()
        {
        }
    }
}
