using System;

namespace Bing.ElasticSearch.Tests.Samples
{
    /// <summary>
    /// 客户
    /// </summary>
    public class Customer
    {
        /// <summary>
        /// 编号
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 年龄
        /// </summary>
        public int Age { get; set; }
    }
}
