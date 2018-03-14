using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Domains.Entities
{
    /// <summary>
    /// 变更值
    /// </summary>
    public class ChangeValue
    {
        #region 属性

        /// <summary>
        /// 属性名
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 旧值
        /// </summary>
        public string OldValue { get; }

        /// <summary>
        /// 新值
        /// </summary>
        public string NewValue { get; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="ChangeValue"/>类型的实例
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="description">描述</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        public ChangeValue(string propertyName, string description, string oldValue, string newValue)
        {
            PropertyName = propertyName;
            Description = description;
            OldValue = oldValue;
            NewValue = newValue;
        }

        #endregion

        /// <summary>
        /// 输出变更信息
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var result = new StringBuilder();
            result.AppendFormat("{0}({1}),", PropertyName, Description);
            result.AppendFormat("旧值:{0},新值:{1}", OldValue, NewValue);
            return result.ToString();
        }
    }
}
