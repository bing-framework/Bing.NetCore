using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Domains.Entities.Trees
{
    /// <summary>
    /// 树型节点启用
    /// </summary>
    public interface IEnabled
    {
        /// <summary>
        /// 启用
        /// </summary>
        bool Enabled { get; set; }
    }
}
