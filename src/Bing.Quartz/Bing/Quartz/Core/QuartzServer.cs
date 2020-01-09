using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.Quartz.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;

namespace Bing.Quartz.Core
{
    /// <summary>
    /// Quartz服务
    /// </summary>
    public class QuartzServer : IQuartzServer
    {
        /// <summary>
        /// 日志记录器
        /// </summary>
        private ILogger _logger;

        /// <summary>
        /// 属性集合
        /// </summary>
        private readonly NameValueCollection _props;

        /// <summary>
        /// 调度器
        /// </summary>
        private IScheduler _scheduler;

        /// <summary>
        /// 服务提供程序
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 初始化一个<see cref="QuartzServer"/>类型的实例
        /// </summary>
        /// <param name="props">属性集合</param>
        /// <param name="serviceProvider">服务提供程序</param>
        public QuartzServer(NameValueCollection props, IServiceProvider serviceProvider)
        {
            _props = props;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 启动
        /// </summary>
        /// <param name="cancellation">取消令牌</param>
        public async Task StartAsync(CancellationToken cancellation = default)
        {
            if (_scheduler != null)
                return;
            _logger = _serviceProvider.GetService<ILogger<QuartzServer>>();
            // 调度器工厂
            var factory = _props != null ? new StdSchedulerFactory(_props) : new StdSchedulerFactory();
            // 创建一个调度器
            _scheduler = await factory.GetScheduler(cancellation);
            // 绑定自定义任务工厂
            _scheduler.JobFactory = new JobFactory(_serviceProvider);
            // 添加任务监听器
            AddJobListener();
            // 添加触发器监听器
            AddTriggerListener();
            // 添加调度器监听器
            AddSchedulerListener();
            // 启动
            await _scheduler.Start(cancellation);
            _logger.LogInformation("Quartz服务启动");
        }

        /// <summary>
        /// 添加任务监听器
        /// </summary>
        private void AddJobListener()
        {
            var jobListeners = _serviceProvider.GetServices<IJobListener>().ToList();
            if (!jobListeners.Any())
                return;
            foreach (var listener in jobListeners)
                _scheduler.ListenerManager.AddJobListener(listener);
        }

        /// <summary>
        /// 添加触发器监听器
        /// </summary>
        private void AddTriggerListener()
        {
            var triggerListeners = _serviceProvider.GetServices<ITriggerListener>().ToList();
            if (!triggerListeners.Any())
                return;
            foreach (var listener in triggerListeners)
                _scheduler.ListenerManager.AddTriggerListener(listener);
        }

        /// <summary>
        /// 添加调度器监听器
        /// </summary>
        private void AddSchedulerListener()
        {
            var schedulerListeners = _serviceProvider.GetServices<ISchedulerListener>().ToList();
            if (!schedulerListeners.Any())
                return;
            foreach (var listener in schedulerListeners)
                _scheduler.ListenerManager.AddSchedulerListener(listener);
        }

        /// <summary>
        /// 停止
        /// </summary>
        /// <param name="cancellation">取消令牌</param>
        public async Task StopAsync(CancellationToken cancellation = default)
        {
            if (_scheduler == null)
                return;
            await _scheduler.Shutdown(true, cancellation);
            _logger.LogInformation("Quartz服务停止");
        }

        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="jobDetail">任务详情</param>
        /// <param name="trigger">触发器</param>
        /// <param name="cancellation">取消令牌</param>
        public async Task AddJobAsync(IJobDetail jobDetail, ITrigger trigger, CancellationToken cancellation = default) => await _scheduler.ScheduleJob(jobDetail, trigger, cancellation);

        /// <summary>
        /// 暂停任务
        /// </summary>
        /// <param name="jobKey">任务键</param>
        /// <param name="cancellation">取消令牌</param>
        public async Task PauseJobAsync(JobKey jobKey, CancellationToken cancellation = default) => await _scheduler.PauseJob(jobKey, cancellation);

        /// <summary>
        /// 恢复任务
        /// </summary>
        /// <param name="jobKey">任务键</param>
        /// <param name="cancellation">取消令牌</param>
        public async Task ResumeJobAsync(JobKey jobKey, CancellationToken cancellation = default) => await _scheduler.ResumeJob(jobKey, cancellation);

        /// <summary>
        /// 删除任务
        /// </summary>
        /// <param name="jobKey">任务键</param>
        /// <param name="cancellation">取消令牌</param>
        public async Task DeleteJobAsync(JobKey jobKey, CancellationToken cancellation = default) => await _scheduler.DeleteJob(jobKey, cancellation);
    }
}
