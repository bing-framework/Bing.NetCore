using System.Collections.Generic;
using Bing.Utils;

namespace Bing.Logs.Abstractions
{
    /// <summary>
    /// 日志转换器
    /// </summary>
    public interface ILogConvert
    {
        /// <summary>
        /// 转换
        /// </summary>
        List<Item> To();
    }
}
