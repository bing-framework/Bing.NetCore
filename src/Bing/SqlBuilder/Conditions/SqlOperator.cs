using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Bing.SqlBuilder.Conditions
{
    /// <summary>
    /// Sql操作符
    /// </summary>
    public enum SqlOperator
    {
        /// <summary>
        /// 等于 =
        /// </summary>
        [Description("等于")]
        Equal,
        /// <summary>
        /// 不等于 &lt;&gt;
        /// </summary>
        [Description("不等于")]
        NotEqual,
        /// <summary>
        /// 大于 &gt;
        /// </summary>
        [Description("大于")]
        GreaterThan,
        /// <summary>
        /// 大于等于 &gt;=
        /// </summary>
        [Description("大于等于")]
        GreaterEqual,
        /// <summary>
        /// 小于 &lt;
        /// </summary>
        [Description("小于")]
        LessThan,
        /// <summary>
        /// 小于等于 &lt;=
        /// </summary>
        [Description("小于等于")]
        LessEqual,
        /// <summary>
        /// 头匹配
        /// </summary>
        [Description("头匹配")]
        Starts,
        /// <summary>
        /// 尾匹配
        /// </summary>
        [Description("尾匹配")]
        Ends,
        /// <summary>
        /// 模糊匹配 like
        /// </summary>
        [Description("模糊匹配")]
        Contains,
        /// <summary>
        /// 模糊非匹配 not like
        /// </summary>
        [Description("模糊非匹配")]
        NotContains,
        /// <summary>
        /// In
        /// </summary>
        [Description("In")]
        In,
        /// <summary>
        /// Not In
        /// </summary>
        [Description("Not In")]
        NotIn,
        /// <summary>
        /// 范围
        /// </summary>
        [Description("范围")]
        Between,
        /// <summary>
        /// Is Null
        /// </summary>
        [Description("Is Null")]
        IsNull,
        /// <summary>
        /// Is Not Null
        /// </summary>
        [Description("Is Not Null")]
        IsNotNull
    }
}
