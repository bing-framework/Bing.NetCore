using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Bing.Utils.Develops
{
    /// <summary>
    /// 代码性能计时器
    /// </summary>
    public class CodeTimer
    {
        /// <summary>
        /// 基准时间
        /// </summary>
        private static double _msBase;

        /// <summary>
        /// 是否支持CPU周期
        /// </summary>
        private static bool _supportCycle = true;

        /// <summary>
        /// CPU周期
        /// </summary>
        private ulong _cpuCycles = 0;

        /// <summary>
        /// 线程时间。单位：毫秒
        /// </summary>
        private long _threadTime = 0;

        /// <summary>
        /// GC代数
        /// </summary>
        private int[] _gen;

        /// <summary>
        /// 线程
        /// </summary>
        private Thread _thread;

        /// <summary>
        /// 初始化一个<see cref="CodeTimer"/>类型的实例
        /// </summary>
        public CodeTimer() => Gen = new[] { 0, 0, 0 };

        /// <summary>
        /// 输出内容
        /// </summary>
        public static Action<string> WriteLine { get; set; } = Console.Write;

        /// <summary>
        /// 次数
        /// </summary>
        public int Times { get; set; }

        /// <summary>
        /// 迭代方法。如不指定，则使用Time(int index)
        /// </summary>
        public Action<int> Action { get; set; }

        /// <summary>
        /// 是否显示控制台进度
        /// </summary>
        public bool ShowProgress { get; set; }

        /// <summary>
        /// 进度
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// CPU周期
        /// </summary>
        public long CpuCycles { get; set; }

        /// <summary>
        /// 线程时间。单位：毫秒
        /// </summary>
        public long ThreadTime { get; set; }

        /// <summary>
        /// GC代数
        /// </summary>
        public int[] Gen { get; set; }

        /// <summary>
        /// 执行时间
        /// </summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>
        /// 计时。核心方法，处理进程和线程优先级
        /// </summary>
        public virtual void Time()
        {
            if (Times <= 0)
                throw new ArgumentException("非法迭代次数");
            // 设定进程、线程优先级，并在完成时还原
            var pp = Process.GetCurrentProcess().PriorityClass;
            var tp = Thread.CurrentThread.Priority;
            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
                StartProgress();
                TimeTrue();
            }
            finally
            {
                StopProgress();
                Thread.CurrentThread.Priority = tp;
                Process.GetCurrentProcess().PriorityClass = pp;
            }
        }

        /// <summary>
        /// 实际计时
        /// </summary>
        protected virtual void TimeTrue()
        {
            if (Times <= 0)
                throw new ArgumentException("非法迭代次数");
            // 统计GC代数
            GC.Collect(GC.MaxGeneration);
            _gen = new int[GC.MaxGeneration + 1];
            for (var i = 0; i <= GC.MaxGeneration; i++)
                _gen[i] = GC.CollectionCount(i);
            var watch = Stopwatch.StartNew();
            _cpuCycles = GetCycleCount();
            _threadTime = GetCurrentThreadTimes();

            // 如果未指定迭代方法，则使用内部的Time
            var action = Action;
            if (action == null)
            {
                action = Time;
                Init();
            }
            for (var i = 0; i < Times; i++)
            {
                Index = i;
                action(i);
            }
            if (Action == null)
                Finish();

            CpuCycles = (long)(GetCycleCount() - _cpuCycles);
            // 线程时间，单位：100ns，除以10000转为ms
            ThreadTime = (GetCurrentThreadTimes() - _threadTime) / 10_000;
            watch.Stop();
            Elapsed = watch.Elapsed;
            // 统计GC代数
            var list = new List<int>();
            for (var i = 0; i <= GC.MaxGeneration; i++)
            {
                var count = GC.CollectionCount(i) - _gen[i];
                list.Add(count);
            }
            Gen = list.ToArray();
        }

        /// <summary>
        /// 执行一次迭代，预热所有方法
        /// </summary>
        public void TimeOne()
        {
            var n = Times;
            try
            {
                Times = 1;
                Time();
            }
            finally
            {
                Times = n;
            }
        }

        /// <summary>
        /// 迭代前执行，计算时间
        /// </summary>
        public virtual void Init() { }

        /// <summary>
        /// 每一次迭代，计算时间
        /// </summary>
        /// <param name="index">迭代索引</param>
        public virtual void Time(int index) { }

        /// <summary>
        /// 迭代后执行，计算时间
        /// </summary>
        public virtual void Finish() { }

        /// <summary>
        /// 开始进程
        /// </summary>
        private void StartProgress()
        {
            if (!ShowProgress)
                return;
            // 使用低优先级线程显示进度
            _thread = new Thread(Progress)
            {
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };
            _thread.Start();
        }

        /// <summary>
        /// 停止进程
        /// </summary>
        private void StopProgress()
        {
            if (_thread == null || !_thread.IsAlive)
                return;
            _thread.Abort();
            _thread.Join(3000);
        }

        /// <summary>
        /// 执行进程中
        /// </summary>
        /// <param name="state">状态</param>
        private void Progress(object state)
        {
            var left = Console.CursorLeft;
            // 设置光标不可见
            var cursorVisible = Console.CursorVisible;
            Console.CursorVisible = false;
            var sw = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    var i = Index;
                    if (i >= Times)
                        break;
                    if (i > 0 && sw.Elapsed.TotalMilliseconds > 10)
                    {
                        var prog = (double)i / Times;
                        var ms = sw.Elapsed.TotalMilliseconds;
                        // 预计总时间
                        var ts = new TimeSpan(0, 0, 0, 0, (int)(ms * Times / i));
                        var speed = i / ms;
                        var cost = ms / i;
                        WriteLine($"{ms,7:n0}ms {prog:p2} Total=>{ts}");
                        Console.CursorLeft = left;
                    }
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch
                {
                    break;
                }
                Thread.Sleep(500);
            }
            sw.Stop();
            Console.CursorLeft = left;
            Console.CursorVisible = cursorVisible;
        }

        /// <summary>
        /// 输出字符串。输出依次分别是：执行时间、CPU线程时间、时钟周期、GC代数
        /// </summary>
        public override string ToString()
        {
            var ms = Elapsed.TotalMilliseconds;
            if (_msBase == 0)
                _msBase = ms;
            var pc = ms / _msBase;
            var speed = ms == 0 ? 0 : Times / ms;
            var cost = Times == 0 ? 0 : ms / Times;
            return $"{ms,7:n0}ms {ThreadTime,7:n0}ms {CpuCycles,15:n0} {Gen[0],3}/{Gen[1]}/{Gen[2]}\t{pc,8:p2}";
        }

        /// <summary>
        /// 计时
        /// </summary>
        /// <param name="times">次数</param>
        /// <param name="action">需要计时的委托</param>
        /// <param name="needTimeOne">是否需要预热</param>
        public static CodeTimer Time(int times, Action<int> action, bool needTimeOne = true)
        {
            var timer = new CodeTimer() { Times = times, Action = action, };
            if (needTimeOne)
                timer.TimeOne();
            timer.Time();
            return timer;
        }

        /// <summary>
        /// 计时，并用控制台输出行
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="times">次数</param>
        /// <param name="action">需要计时的委托</param>
        /// <param name="needTimeOne">是否需要预热</param>
        public static CodeTimer Timeline(string title, int times, Action<int> action, bool needTimeOne = true)
        {
            var n = Encoding.UTF8.GetByteCount(title);
            WriteLine($"{(n >= 16 ? "" : new string(' ', 16 - n))}{title}: ");
            var timer = new CodeTimer()
            {
                Times = times,
                Action = action,
                ShowProgress = true,
            };
            var currentForeColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            var left = Console.CursorLeft;
            if (needTimeOne)
                timer.TimeOne();
            timer.Time();
            // 等一会，让进度那边先输出
            Thread.Sleep(10);
            Console.CursorLeft = left;
            WriteLine($"{timer}\r\n");
            Console.ForegroundColor = currentForeColor;
            return timer;
        }

        /// <summary>
        /// 显示头部
        /// </summary>
        /// <param name="title">标题</param>
        public static void ShowHeader(string title = "指标")
        {
            Write(title, 16);
            WriteLine(": ");
            var currentForeColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Write("执行时间", 9);
            WriteLine(" ");
            Write("CPU时间", 9);
            WriteLine(" ");
            Write("指令周期", 15);
            Write("GC(0/1/2)", 9);
            WriteLine("   百分比\r\n");
            _msBase = 0;
            Console.ForegroundColor = currentForeColor;
        }

        /// <summary>
        /// 写入
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="max">最大长度</param>
        private static void Write(string name, int max)
        {
            var len = Encoding.UTF8.GetByteCount(name);
            if (len < max)
                WriteLine(new string(' ', max - len));
            WriteLine(name);
        }

        /// <summary>
        /// 获取CPU周期计数
        /// </summary>
        private static ulong GetCycleCount()
        {
            if (!_supportCycle)
                return 0;
            try
            {
                ulong cycleCount = 0;
                NativeMethods.QueryThreadCycleTime(NativeMethods.GetCurrentThread(), ref cycleCount);
                return cycleCount;
            }
            catch
            {
                _supportCycle = false;
                return 0;
            }
        }

        /// <summary>
        /// 获取当前线程时间
        /// </summary>
        private static long GetCurrentThreadTimes()
        {
            NativeMethods.GetThreadTimes(NativeMethods.GetCurrentThread(), out var ct, out var et, out var kernelTime,
                out var userTimer);
            return kernelTime + userTimer;
        }
    }

    /// <summary>
    /// 本地方法
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// 查询线程循环时间
        /// </summary>
        /// <param name="threadHandle">线程句柄</param>
        /// <param name="cycleTime">循环时间</param>
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryThreadCycleTime(IntPtr threadHandle, ref ulong cycleTime);

        /// <summary>
        /// 获取当前线程
        /// </summary>
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentThread();

        /// <summary>
        /// 获取线程时间
        /// </summary>
        /// <param name="hThread">线程句柄</param>
        /// <param name="lpCreationTime">创建时间</param>
        /// <param name="lpExitTime">退出时间</param>
        /// <param name="lpKernelTime">内核时间</param>
        /// <param name="lpUserTime">使用时间</param>
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetThreadTimes(IntPtr hThread, out long lpCreationTime, out long lpExitTime,
            out long lpKernelTime, out long lpUserTime);
    }
}
