using System;
using Bing.Utils.Helpers;

namespace Bing.Utils
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
        public DisposeAction(Action action)
        {
            Check.NotNull(action, nameof(action));
            _action = action;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _action();
        }
    }
}
