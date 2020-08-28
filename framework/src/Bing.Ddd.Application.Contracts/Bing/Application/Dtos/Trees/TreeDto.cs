using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Bing.Application.Dtos
{
    /// <summary>
    /// 树型数据传输对象
    /// </summary>
    /// <typeparam name="TNode">树节点类型</typeparam>
    public class TreeDto<TNode> : TreeDto where TNode : TreeDto<TNode>
    {
        /// <summary>
        /// 子节点列表
        /// </summary>
        public List<TNode> Children { get; set; }

        /// <summary>
        /// 初始化一个<see cref="TreeDto{TNode}"/>类型的实例
        /// </summary>
        public TreeDto() => Children = new List<TNode>();
    }

    /// <summary>
    /// 树型数据传输对象
    /// </summary>
    public class TreeDto : TreeDtoBase
    {
        /// <summary>
        /// 标签文本
        /// </summary>
        public virtual string Text { get; set; }

        /// <summary>
        /// 图标
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// 是否禁用复选框
        /// </summary>
        public bool? DisableCheckbox { get; set; }

        /// <summary>
        /// 是否可选中
        /// </summary>
        public bool? Selectable { get; set; } = true;

        /// <summary>
        /// 复选框是否被勾选
        /// </summary>
        public bool? Checked { get; set; }

        /// <summary>
        /// 节点是否被选中
        /// </summary>
        public bool? Selected { get; set; }

        /// <summary>
        /// 是否叶节点
        /// </summary>
        public bool? Leaf { get; set; }
    }
}
