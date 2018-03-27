using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Bing.Utils.Develops
{
    /// <summary>
    /// 单元测试辅助操作
    /// </summary>
    public static class UnitTester
    {
        /// <summary>
        /// 输出内容
        /// </summary>
        public static Action<string> WriteLine { get; set; } = Console.WriteLine;

        #region TestConcurrency(并发测试)
        /// <summary>
        /// 并发测试
        /// </summary>
        /// <param name="action">各线程执行的方法</param>
        /// <param name="threadNumber">启动线程数，默认1个</param>
        public static void TestConcurrency(Action action, int threadNumber = 1)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            WriteLine("并发模拟测试开始");
            var threads = new List<Thread>();
            var resetEvent = new ManualResetEvent(false);
            for (int i = 0; i < threadNumber; i++)
            {
                var thread = new Thread(number =>
                {
                    WriteLine(string.Format("线程{0}执行挂起操作,线程号：{1},耗时：{2}秒", number,
                        Thread.CurrentThread.ManagedThreadId.ToString(), stopwatch.Elapsed.TotalSeconds));
                    resetEvent.WaitOne();
                    action();
                    WriteLine(string.Format("线程{0}执行任务完成,线程号：{1},耗时：{2}秒", number,
                        Thread.CurrentThread.ManagedThreadId.ToString(), stopwatch.Elapsed.TotalSeconds));
                });
                thread.Start(i + 1);
                threads.Add(thread);
            }
            WriteLine("暂停50毫秒后唤醒所有线程");
            Thread.Sleep(50);
            resetEvent.Set();
            threads.ForEach(t => t.Join());
            WriteLine(string.Format("执行完成,即将退出，耗时：{0}秒", stopwatch.Elapsed.TotalSeconds));
        }
        #endregion
    }
}
