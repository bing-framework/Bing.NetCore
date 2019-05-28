using System.Collections.Generic;
using System.Linq;
using Bing.Utils.Extensions;

namespace Bing.Webs.Controllers.Trees
{
    /// <summary>
    /// 树型表格结果
    /// </summary>
    /// <typeparam name="TNode">树型节点类型</typeparam>
    public class TreeTableResult<TNode> : ITreeTableResult<TNode> where TNode : TreeDto<TNode>
    {
        /// <summary>
        /// 树型参数列表
        /// </summary>
        private readonly IEnumerable<TNode> _data;

        /// <summary>
        /// 树型表格结果
        /// </summary>
        private readonly List<TNode> _result;

        /// <summary>
        /// 是否异步加载
        /// </summary>
        private readonly bool _async;

        /// <summary>
        /// 初始化一个<see cref="TreeTableResult{TNode}"/>类型的实例
        /// </summary>
        /// <param name="data">树型参数列表</param>
        /// <param name="async">是否异步加载</param>
        public TreeTableResult(IEnumerable<TNode> data, bool async = false)
        {
            _data = data;
            _async = async;
            _result = new List<TNode>();
        }

        /// <summary>
        /// 获取树型表格结果
        /// </summary>
        public List<TNode> GetResult()
        {
            if (_data == null)
            {
                return _result;
            }

            foreach (var root in _data.Where(IsRoot).OrderBy(t => t.SortId))
            {
                AddNode(root);
            }

            return _result;
        }

        /// <summary>
        /// 是否根节点
        /// </summary>
        /// <param name="dto">节点</param>
        protected virtual bool IsRoot(TNode dto)
        {
            if (_data.Any(t => t.ParentId.IsEmpty()))
            {
                return dto.ParentId.IsEmpty();
            }

            return dto.Level == _data.Min(t => t.Level);
        }

        /// <summary>
        /// 添加节点
        /// </summary>
        /// <param name="node">节点</param>
        private void AddNode(TNode node)
        {
            if (node == null)
            {
                return;
            }
            Init(node);
            _result.Add(node);
            var children = GetChildren(node);
            foreach (var child in children)
            {
                AddNode(child);
            }
        }

        /// <summary>
        /// 初始化节点
        /// </summary>
        /// <param name="node">节点</param>
        protected virtual void Init(TNode node)
        {
            InitExpanded(node);
            InitLeaf(node);
        }

        /// <summary>
        /// 初始化节点展开状态
        /// </summary>
        /// <param name="node">节点</param>
        protected virtual void InitExpanded(TNode node)
        {
            if (node.Expanded == null)
            {
                node.Expanded = false;
                return;
            }

            if (node.Level == 1)
            {
                node.Expanded = true;
            }
        }

        /// <summary>
        /// 初始化叶节点状态
        /// </summary>
        /// <param name="node">节点</param>
        protected virtual void InitLeaf(TNode node)
        {
            node.Leaf = false;
            if (_async)
            {
                return;
            }

            if (IsLeaf(node))
            {
                node.Leaf = true;
            }
        }

        /// <summary>
        /// 是否叶节点
        /// </summary>
        /// <param name="node">节点</param>
        protected virtual bool IsLeaf(TNode node)
        {
            if (node.Id.IsEmpty())
            {
                return true;
            }

            return _data.All(t => t.ParentId != node.Id);
        }

        /// <summary>
        /// 获取节点直接下级
        /// </summary>
        /// <param name="node">节点</param>
        private List<TNode> GetChildren(TNode node)
        {
            return _data.Where(t => t.ParentId == node.Id).OrderBy(t => t.SortId).ToList();
        }
    }
}
