using System.Collections.Generic;

namespace Bing.Webs.Controllers.Trees
{
    /// <summary>
    /// 树型表格结果
    /// </summary>
    public interface ITreeTableResult<TNode> where TNode : TreeDto<TNode>
    {
        /// <summary>
        /// 获取树型表格结果
        /// </summary>
        List<TNode> GetResult();
    }
}
