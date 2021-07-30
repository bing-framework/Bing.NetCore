using System.Collections.Generic;
using CanalSharp.Protocol;

namespace Bing.Canal.Model
{
    /// <summary>
    /// 数据变更
    /// </summary>
    public class DataChange
    {
        /// <summary>
        /// 数据库名称
        /// </summary>
        public string DbName { get; set; }

        /// <summary>
        /// 表名
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// 事件类型
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// Canal目的地
        /// </summary>
        public string CanalDestination { get; set; }

        /// <summary>
        /// 变更前
        /// </summary>
        public List<Column> BeforeColumnList { get; set; }

        /// <summary>
        /// 变更后
        /// </summary>
        public List<Column> AfterColumnList { get; set; }
    }
}
