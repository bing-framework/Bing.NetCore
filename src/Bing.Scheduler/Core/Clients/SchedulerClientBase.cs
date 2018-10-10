using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Bing.Scheduler.Abstractions.Clients;

namespace Bing.Scheduler.Core.Clients
{
    /// <summary>
    /// 任务调度客户端基类
    /// </summary>
    public abstract class SchedulerClientBase:ISchedulerClient
    {
        /// <summary>
        /// 类名映射类型字典
        /// </summary>
        protected readonly Dictionary<string, Type> ClassNameMapTypes = new Dictionary<string, Type>();

        /// <summary>
        /// 运行任务字典
        /// </summary>
        protected readonly ConcurrentDictionary<string, object>
            RunningJobs = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// 服务连接重试次数
        /// </summary>
        protected int _retryTimes;

        /// <summary>
        /// 任务分组
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// 任务调度中心服务地址
        /// </summary>
        public string Service { get; set; }

        /// <summary>
        /// 是否忽略正在运行的任务
        /// </summary>
        public bool BypassRunning { get; set; } = true;

        /// <summary>
        /// 服务连接重试次数
        /// </summary>
        public int RetryTimes { get; set; } = 3600;

        /// <summary>
        /// 初始化一个<see cref="SchedulerClientBase"/>类型的实例
        /// </summary>
        protected SchedulerClientBase() { }

        /// <summary>
        /// 初始化一个<see cref="SchedulerClientBase"/>类型的实例
        /// </summary>
        /// <param name="service">任务调度中心服务地址</param>
        /// <param name="group">任务分组</param>
        protected SchedulerClientBase(string service, string group) : this()
        {
            Group = group;
            Service = new Uri(service).ToString();
        }

        /// <summary>
        /// 初始化并启动
        /// </summary>
        public void Init()
        {
            CheckArguments();
            ScanAssemblies();
            if (ClassNameMapTypes.Count == 0)
            {
                Debug.WriteLine("在此应用程序中未检测到任务作业");
                return;
            }
            RunningJobs.Clear();
            ConnectScheduler();
        }

        /// <summary>
        /// 连接任务调度器
        /// </summary>
        protected abstract void ConnectScheduler();

        /// <summary>
        /// 扫描程序集
        /// </summary>
        protected abstract void ScanAssemblies();

        /// <summary>
        /// 检查参数
        /// </summary>
        private void CheckArguments()
        {
            if (string.IsNullOrWhiteSpace(Group))
            {
                throw new ArgumentNullException(nameof(Group));
            }

            if (string.IsNullOrWhiteSpace(Service))
            {
                throw new ArgumentNullException(nameof(Service));
            }
        }
    }
}
