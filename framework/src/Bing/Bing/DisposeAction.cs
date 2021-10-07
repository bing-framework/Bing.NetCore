using System;

namespace Bing
{
    /// <summary>
    /// 释放操作
    /// </summary>
    public class DisposeAction : IDisposable
    {
        /// <summary>
        /// 操作
        /// </summary>
        private readonly Action _action;

        /// <summary>
        /// 初始化一个<see cref="DisposeAction"/>类型的实例
        /// </summary>
        /// <param name="action">操作</param>
        public DisposeAction(Action action) => _action = action ?? throw new ArgumentNullException(nameof(action));

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose() => _action();
    }
}
