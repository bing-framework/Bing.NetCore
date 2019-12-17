using System;

namespace Bing.Utils
{
    /// <summary>
    /// 空释放
    /// </summary>
    public sealed class NullDisposable : IDisposable
    {
        /// <summary>
        /// 实例
        /// </summary>
        public static NullDisposable Instance { get; } = new NullDisposable();

        /// <summary>
        /// 初始化一个<see cref="NullDisposable"/>类型的实例
        /// </summary>
        private NullDisposable() { }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose() { }
    }
}
