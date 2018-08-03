using System;

namespace Bing.Offices.Excels.Attributes
{
    /// <summary>
    /// 标记为Excel创建实体忽略，防止死循环的产生
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ExcelIgnoreAttribute:Attribute
    {
    }
}
