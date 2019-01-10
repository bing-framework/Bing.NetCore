using System;
using System.ComponentModel.DataAnnotations;
using Bing.Domains.Entities;

namespace Bing.Datas.Persistence
{
    /// <summary>
    /// 持久化对象基类
    /// </summary>
    public abstract class PersistentObjectBase : PersistentObjectBase<Guid>
    {
    }

    /// <summary>
    /// 持久化对象基类
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    public abstract class PersistentObjectBase<TKey> : PersistentEntityBase<TKey>, IVersion
    {
        /// <summary>
        /// 版本号（乐观锁）
        /// </summary>
        public byte[] Version { get; set; }
    }
}
