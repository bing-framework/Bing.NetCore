using System;

namespace Bing.Offices.Excels.Attributes
{
    /// <summary>
    /// 标记是不是导出Excel，标记为实体类
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ExcelEntityAttribute:Attribute
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
        /// 如果等于true，Name必须有值，Excel的表头会变成联航，同时该Excel内部数据不参与总排序，排序用下面这个来代替，内部再排序
        /// 排序取当前最小值排序
        /// </summary>
        public bool Show { get; set; }
    }
}
