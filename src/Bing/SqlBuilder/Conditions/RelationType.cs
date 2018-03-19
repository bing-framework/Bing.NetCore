using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Bing.SqlBuilder.Conditions
{
    /// <summary>
    /// 关联类型：And、Or
    /// </summary>
    public enum RelationType
    {
        /// <summary>
        /// And
        /// </summary>
        [Description("And")]
        And,
        /// <summary>
        /// Or
        /// </summary>
        [Description("Or")]
        Or
    }
}
