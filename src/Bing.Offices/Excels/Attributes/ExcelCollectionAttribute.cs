using System;
using System.Collections.Generic;

namespace Bing.Offices.Excels.Attributes
{
    /// <summary>
    /// Excel集合
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ExcelCollectionAttribute:Attribute
    {
        /// <summary>
        /// 导出ID，用于限定导出字段，处理一个类对应不同名称的情况
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 导出时，对应数据库的字段，主要是用户区分每个字段，不能有特性重名的导出时的列名。
        /// 导出排序跟定义了特性的字段的顺序有关，可以使用a_id,b_id来确实是否使用
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 排序号，展示到第几个可以使用a_id,b_id来确定不同排序
        /// </summary>
        public int OrderNum { get; set; } = 0;

        /// <summary>
        /// 创建时创建的类型
        /// </summary>
        public Type Type { get; set; } = typeof(List<>);
    }
}
