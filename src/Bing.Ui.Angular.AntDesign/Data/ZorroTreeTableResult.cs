using System.Collections.Generic;

namespace Bing.Ui.Data
{
    /// <summary>
    /// Ng-Zorro树形表格结果数据
    /// </summary>
    /// <typeparam name="TNode">树节点类型</typeparam>
    public class ZorroTreeTableResult<TNode> : TreeDto where TNode : TreeDto
    {
        /// <summary>
        /// 子节点列表
        /// </summary>
        public List<TNode> Children { get; set; }

        /// <summary>
        /// 初始化一个<see cref="ZorroTreeTableResult{TNode}"/>类型的实例
        /// </summary>
        public ZorroTreeTableResult()
        {
            Children = new List<TNode>();
        }
    }
}
