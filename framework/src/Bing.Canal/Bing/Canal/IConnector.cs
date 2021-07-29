using System.Collections.Generic;
using CanalSharp.Protocol;

namespace Bing.Canal
{
    /// <summary>
    /// 连接器
    /// </summary>
    public interface IConnector
    {
        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="entries">入口对象列表</param>
        /// <param name="formatter">格式化器</param>
        bool Process(List<Entry> entries, IFormatter formatter);
    }
}
