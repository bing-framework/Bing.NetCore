using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Bing.Scheduler.Abstractions.Clients;
using Bing.Scheduler.Core;
using Bing.Scheduler.Core.Clients;
using Bing.Scheduler.Exceptions;
using Microsoft.AspNetCore.SignalR.Client;

namespace Bing.Scheduler.Client
{
    /// <summary>
    /// 任务调度客户端
    /// </summary>
    public class SchedulerClient:SchedulerClientBase
    {
        /// <summary>
        /// 连接任务调度器
        /// </summary>
        protected override void ConnectScheduler()
        {
            var times = Interlocked.Increment(ref _retryTimes);
            while (times<=RetryTimes)
            {
                var connection = new HubConnectionBuilder().WithUrl($"{Service}client/?group={Group}").Build();

                try
                {
                    OnClose(connection);
                    OnFire(connection);
                    OnWatchCallback(connection);
                    connection.StartAsync().Wait();
                    connection.SendAsync("Watch", ClassNameMapTypes.Keys.ToArray()).Wait();
                }
                catch (Exception e) when(e.InnerException?.InnerException is SocketException)
                {
                    connection.StopAsync().Wait();
                    connection.DisposeAsync().Wait();
                    var exception = (SocketException) e.InnerException.InnerException;
                    if (exception.SocketErrorCode == SocketError.TimedOut ||
                        exception.SocketErrorCode == SocketError.ConnectionRefused)
                    {
                        Thread.Sleep(1000);
                        if (times <= RetryTimes)
                        {
                            Debug.WriteLine("重新连接任务调度器");
                            continue;
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 监听回调
        /// </summary>
        /// <param name="connection">连接</param>
        private void OnWatchCallback(HubConnection connection)
        {
            connection.On<bool>("WatchCallback", (isSuccess) =>
            {
                if (!isSuccess)
                {
                    connection.StopAsync().Wait();
                    connection.DisposeAsync().Wait();
                    throw new SchedulerException("监听回调失败");
                }
            });
        }

        /// <summary>
        /// 监听关闭
        /// </summary>
        /// <param name="connection">连接</param>
        private void OnClose(HubConnection connection)
        {
            connection.Closed += e =>
            {
                if (e == null || ((WebSocketException) e).WebSocketErrorCode ==
                    WebSocketError.ConnectionClosedPrematurely)
                {
                    ConnectScheduler();
                }

                return Task.CompletedTask;
            };
        }

        /// <summary>
        /// 监听触发
        /// </summary>
        /// <param name="connection">连接</param>
        private void OnFire(HubConnection connection)
        {
            connection.On<JobContext, string>("Fire", (context, batchId) =>
            {
                var className = context?.Name;
                try
                {
                    bool shouldFire = false;
                    if (string.IsNullOrWhiteSpace(className) || !ClassNameMapTypes.ContainsKey(className) ||
                        (BypassRunning && RunningJobs.ContainsKey(className)))
                    {
                        return;
                    }

                    if ((context.FireTime - DateTime.Now).TotalSeconds <= 10)
                    {
                        shouldFire = true;
                    }
                    else
                    {
                        Debug.WriteLine($"触发任务超时: {className}");
                    }

                    if (shouldFire)
                    {
                        connection.SendAsync("FireCallback", batchId, context.Id, JobStatus.Running).Wait();

                        var jobType = ClassNameMapTypes[className];
                        RunningJobs.TryAdd(className, null);
                        Task.Factory.StartNew(() =>
                        {
                            bool success = false;
                            try
                            {
                                var jobObject = (IJobProcessor) Activator.CreateInstance(jobType);
                                success = jobObject.Process(context);
                            }
                            catch
                            {
                                // TODO: 输出日志
                            }
                            finally
                            {
                                connection.SendAsync("FireCallback", batchId, context.Id,
                                    success ? JobStatus.Success : JobStatus.Fire).Wait();
                            }
                        }).ContinueWith((t) => { RunningJobs.TryRemove(className, out _); });
                    }
                    else
                    {
                        connection.SendAsync("FireCallback", batchId, context.Id, JobStatus.Bypass).Wait();
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"触发任务 {className} 失败: {e}");
                }
            });
        }

        /// <summary>
        /// 扫描程序集
        /// </summary>
        protected override void ScanAssemblies()
        {
            ClassNameMapTypes.Clear();
            var jobProcessorType = typeof(IJobProcessor);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (string.IsNullOrWhiteSpace(type.FullName))
                    {
                        continue;
                    }

                    if (type.FullName != "Bing.Scheduler.Core.Clients.SimpleJobProcessor" &&
                        type.FullName != "Bing.Scheduler.Abstractions.Clients.IJobProcessor" &&
                        jobProcessorType.IsAssignableFrom(type))
                    {
                        ClassNameMapTypes.Add(type.FullName, type);
                    }
                }
            }
        }
    }
}
