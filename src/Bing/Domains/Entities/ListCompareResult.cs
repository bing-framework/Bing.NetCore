using System.Collections.Generic;

namespace Bing.Domains.Entities
{
    /// <summary>
    /// 列表比较结果
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    public class ListCompareResult<TEntity, TKey> where TEntity : IKey<TKey>
    {
        /// <summary>
        /// 创建列表
        /// </summary>
        public List<TEntity> CreateList { get; }

        /// <summary>
        /// 更新列表
        /// </summary>
        public List<TEntity> UpdateList { get; }

        /// <summary>
        /// 删除列表
        /// </summary>
        public List<TEntity> DeleteList { get; }

        /// <summary>
        /// 初始化一个<see cref="ListCompareResult{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="createList">创建列表</param>
        /// <param name="updateList">更新列表</param>
        /// <param name="deleteList">删除列表</param>
        public ListCompareResult(List<TEntity> createList, List<TEntity> updateList, List<TEntity> deleteList)
        {
            CreateList = createList;
            UpdateList = updateList;
            DeleteList = deleteList;
        }
    }
}
