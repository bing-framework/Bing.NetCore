using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Utils.IdGenerators.Snowflake
{
    /// <summary>
    /// 一次性方法
    /// </summary>
    public class DisposableAction:IDisposable
    {
        /// <summary>
        /// 执行方法
        /// </summary>
        private readonly Action _action;

        /// <summary>
        /// 初始化一个<see cref="DisposableAction"/>类型的实例
        /// </summary>
        /// <param name="action">执行方法</param>
        public DisposableAction(Action action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
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
