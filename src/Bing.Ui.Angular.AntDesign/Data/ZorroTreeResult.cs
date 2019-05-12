using System.Collections.Generic;

namespace Bing.Ui.Data
{
    /// <summary>
    /// Ng-Zorro树形结果数据
    /// </summary>
    public class ZorroTreeResult
    {
        /// <summary>
        /// 树节点列表
        /// </summary>
        public List<ZorroTreeNode> Nodes { get; set; }

        /// <summary>
        /// 展开节点的标识列表
        /// </summary>
        public List<string> ExpandedKeys { get; set; }

        /// <summary>
        /// 复选框选中节点的标识列表
        /// </summary>
        public List<string> CheckedKeys { get; set; }

        /// <summary>
        /// 选中节点的标识列表
        /// </summary>
        public List<string> SelectedKeys { get; set; }

        /// <summary>
        /// 初始化一个<see cref="ZorroTreeResult"/>类型的实例
        /// </summary>
        public ZorroTreeResult()
        {
            Nodes = new List<ZorroTreeNode>();
            ExpandedKeys = new List<string>();
            CheckedKeys = new List<string>();
            SelectedKeys = new List<string>();
        }
    }
}
