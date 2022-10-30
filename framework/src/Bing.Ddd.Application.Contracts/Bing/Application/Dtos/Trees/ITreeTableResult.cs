using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Bing.Application.Dtos;

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