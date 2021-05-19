using System;
using System.Threading;
using Bing.Helpers;

namespace Bing.Tracing
{
    /// <summary>
    /// 跟踪ID上下文
    /// </summary>
    public class TraceIdContext
    {
        /// <summary>
        /// 跟踪标识
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// 根标识
        /// </summary>
        public string RootId { get; set; }

        /// <summary>
        /// 父标识
        /// </summary>
        public string ParentId{ get; set; }

        /// <summary>
        /// 当前标识
        /// </summary>
        public string ChildId{ get; set; }

        /// <summary>
        /// 初始化一个<see cref="TraceIdContext"/>类型的实例
        /// </summary>
        /// <param name="traceId">跟踪标识。如果不传入，则自动生成一个<see cref="Guid"/></param>
        public TraceIdContext(string traceId)
        {
            if (string.IsNullOrEmpty(traceId))
                traceId = Id.NewGuid().ToString();
            TraceId = traceId;
        }

        /// <summary>
        /// 初始化一个<see cref="TraceIdContext"/>类型的实例
        /// </summary>
        /// <param name="traceId">跟踪标识</param>
        /// <param name="rootId">根标识</param>
        /// <param name="parentId">父标识</param>
        /// <param name="childId">当前标识</param>
        public TraceIdContext(string traceId, string rootId, string parentId, string childId)
        {
            TraceId = traceId;
            RootId = rootId;
            ParentId = parentId;
            ChildId = childId;
        }

        /// <summary>
        /// 当前跟踪标识上下文
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static readonly AsyncLocal<TraceIdContext> _currentTraceIdContext = new AsyncLocal<TraceIdContext>();

        /// <summary>
        /// 当前跟踪标识上下文
        /// </summary>
        public static TraceIdContext Current
        {
            get => _currentTraceIdContext.Value;
            set => _currentTraceIdContext.Value = value;
        }
    }
}
