using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Logs.Abstractions
{
    /// <summary>
    /// 日志提供程序工厂
    /// </summary>
    public interface ILogProviderFactory
    {
        /// <summary>
        /// 创建日志提供程序
        /// </summary>
        /// <param name="logName">日志名称</param>
        /// <param name="format">日志格式化器</param>
        /// <returns></returns>
        ILogProvider Create(string logName, ILogFormat format = null);
    }
}
