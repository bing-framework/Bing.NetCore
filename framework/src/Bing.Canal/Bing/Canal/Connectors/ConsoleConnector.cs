using System;
using System.Collections.Generic;
using System.Linq;
using CanalSharp.Protocol;

namespace Bing.Canal.Connectors
{
    /// <summary>
    /// 控制台连接器
    /// </summary>
    public class ConsoleConnector : IConnector
    {
        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="entries">入口对象列表</param>
        /// <param name="formatter">格式化器</param>
        public bool Process(List<Entry> entries, IFormatter formatter)
        {
            foreach (var entry in entries.Where(entry => entry.EntryType == EntryType.Rowdata))
                Console.WriteLine(formatter.Format(entry));
            return true;
        }
    }
}
