using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Domains.Entities
{
    /// <summary>
    /// 键列表比较结果
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public class KeyListCompareResult<TKey>
    {
        /// <summary>
        /// 创建列表
        /// </summary>
        public List<TKey> CreateList { get; }

        /// <summary>
        /// 更新列表
        /// </summary>
        public List<TKey> UpdateList { get; }

        /// <summary>
        /// 删除列表
        /// </summary>
        public List<TKey> DeleteList { get; }

        /// <summary>
        /// 初始化一个<see cref="KeyListCompareResult{TKey}"/>类型的实例
        /// </summary>
        /// <param name="createList">创建列表</param>
        /// <param name="updateList">更新列表</param>
        /// <param name="deleteList">删除列表</param>
        public KeyListCompareResult(List<TKey> createList, List<TKey> updateList, List<TKey> deleteList)
        {
            CreateList = createList;
            UpdateList = updateList;
            DeleteList = deleteList;
        }
    }
}
