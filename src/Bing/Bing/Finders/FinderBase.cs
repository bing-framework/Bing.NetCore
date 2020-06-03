using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Finders
{
    /// <summary>
    /// 查找器基类
    /// </summary>
    /// <typeparam name="TItem">要查找的项类型</typeparam>
    public abstract class FinderBase<TItem> : IFinder<TItem>
    {
        /// <summary>
        /// 对象锁
        /// </summary>
        private readonly object _lockObj = new object();

        /// <summary>
        /// 项缓存
        /// </summary>
        protected readonly List<TItem> ItemsCache = new List<TItem>();

        /// <summary>
        /// 是否已查找过
        /// </summary>
        protected bool Found = false;

        /// <summary>
        /// 查找指定条件的项
        /// </summary>
        /// <param name="predicate">筛选条件</param>
        /// <param name="fromCache">是否来自缓存</param>
        public virtual TItem[] Find(Func<TItem, bool> predicate, bool fromCache = false) => FindAll(fromCache).Where(predicate).ToArray();

        /// <summary>
        /// 查找所有项
        /// </summary>
        /// <param name="fromCache">是否来自缓存</param>
        public virtual TItem[] FindAll(bool fromCache = false)
        {
            lock (_lockObj)
            {
                if (fromCache && Found)
                    return ItemsCache.ToArray();
                var items = FindAllItems();
                Found = true;
                ItemsCache.Clear();
                ItemsCache.AddRange(items);
                return items;
            }
        }

        /// <summary>
        /// 重写已实现所有项的查找
        /// </summary>
        protected abstract TItem[] FindAllItems();
    }
}
