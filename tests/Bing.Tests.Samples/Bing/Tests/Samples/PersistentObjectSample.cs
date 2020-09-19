using System;
using Bing.Data.Persistence;

namespace Bing.Tests.Samples
{
    /// <summary>
    /// 持久化对象测试样例
    /// </summary>
    public class PersistentObjectSample : PersistentEntityBase<Guid>
    {
        /// <summary>
        /// 初始化一个<see cref="PersistentObjectSample"/>类型的实例
        /// </summary>
        public PersistentObjectSample() : this(Guid.Empty) { }

        /// <summary>
        /// 初始化一个<see cref="PersistentObjectSample"/>类型的实例
        /// </summary>
        public PersistentObjectSample(Guid id) => Id = id;

        /// <summary>
        /// 姓名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 编码
        /// </summary>
        public string Code { get; set; }
    }

    /// <summary>
    /// 持久化对象测试样例2
    /// </summary>
    public class PersistentObjectSample2 : PersistentObjectBase<Guid>
    {
        /// <summary>
        /// 初始化一个<see cref="PersistentObjectSample2"/>类型的实例
        /// </summary>
        public PersistentObjectSample2() : this(Guid.Empty) { }

        /// <summary>
        /// 初始化一个<see cref="PersistentObjectSample2"/>类型的实例
        /// </summary>
        public PersistentObjectSample2(Guid id) => Id = id;

        /// <summary>
        /// 姓名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 编码
        /// </summary>
        public string Code { get; set; }
    }
}
