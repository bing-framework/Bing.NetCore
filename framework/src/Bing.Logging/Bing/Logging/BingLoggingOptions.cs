using System;
using System.Collections.Generic;

namespace Bing.Logging
{
    /// <summary>
    /// Bing 日志选项配置
    /// </summary>
    public class BingLoggingOptions
    {
        /// <summary>
        /// 初始化一个<see cref="BingLoggingOptions"/>类型的实例
        /// </summary>
        public BingLoggingOptions()
        {
            ClearProviders = false;
            Extensions = new List<IBingLoggingOptionsExtension>();
        }

        /// <summary>
        /// 日志选项扩展列表
        /// </summary>
        internal IList<IBingLoggingOptionsExtension> Extensions { get; }

        /// <summary>
        /// 是否清空日志提供程序
        /// </summary>
        public bool ClearProviders { get; set; }

        /// <summary>
        /// 注册扩展
        /// </summary>
        /// <param name="extension">日志选项配置扩展</param>
        public void RegisterExtension(IBingLoggingOptionsExtension extension)
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));
            Extensions.Add(extension);
        }
    }
}
