using System;

namespace Bing.Configuration
{
    /// <summary>
    /// 配置类型 特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class OptionsTypeAttribute : Attribute
    {
        /// <summary>
        /// 配置节点名称。默认为：类名称
        /// </summary>
        public string SectionName { get; }

        /// <summary>
        /// 初始化一个<see cref="OptionsTypeAttribute"/>类型的实例
        /// </summary>
        /// <param name="sectionName">配置节点名称</param>
        public OptionsTypeAttribute(string sectionName = null)
        {
            SectionName = sectionName;
        }
    }
}
