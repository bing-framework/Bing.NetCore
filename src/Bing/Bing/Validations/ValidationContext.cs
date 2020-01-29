using System;
using System.Collections.Generic;
using System.Text;
using Bing.Validations.Abstractions;

namespace Bing.Validations
{
    /// <summary>
    /// 验证上下文
    /// </summary>
    /// <typeparam name="TObject">对象类型</typeparam>
    public class ValidationContext<TObject> where TObject:class,IValidatable<TObject>
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

    }
}
