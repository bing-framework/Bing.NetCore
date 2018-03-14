using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bing.Logs
{
    /// <summary>
    /// 日志等级
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 跟踪
        /// </summary>
        [Description("跟踪")]
        Trace = 0,
        /// <summary>
        /// 调试
        /// </summary>
        [Description("调试")]
        Debug = 1,
        /// <summary>
        /// 信息
        /// </summary>
        [Description("信息")]
        Information = 2,
        /// <summary>
        /// 警告
        /// </summary>
        [Description("警告")]
        Warning = 3,
        /// <summary>
        /// 错误
        /// </summary>
        [Description("错误")]
        Error = 4,
        /// <summary>
        /// 致命错误
        /// </summary>
        [Description("致命错误")]
        Fatal = 5,
        /// <summary>
        /// 关闭所有日志，不输出日志
        /// </summary>
        [Description("关闭日志")]
        None = 6
    }
}
