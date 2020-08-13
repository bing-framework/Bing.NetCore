using System;

namespace Bing.Configuration
{
    /// <summary>
    /// 配置类型 特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConfigTypeAttribute : Attribute
    {
        /// <summary>
        /// 配置节点名称。默认为：类名称
        /// </summary>
        public string SectionName { get; }

        /// <summary>
        /// 是否允许重载
        /// </summary>
        public bool ReloadOnChange { get; }

        /// <summary>
        /// 初始化一个<see cref="ConfigTypeAttribute"/>类型的实例
        /// </summary>
        /// <param name="sectionName">配置节点名称</param>
        /// <param name="reloadOnChange">是否允许重载</param>
        public ConfigTypeAttribute(string sectionName = null, bool reloadOnChange = false)
        {
            SectionName = sectionName;
            ReloadOnChange = reloadOnChange;
        }
    }
}
