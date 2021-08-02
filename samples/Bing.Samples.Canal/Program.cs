using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bing.Canal.Server;
using Bing.Canal.Server.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetEscapades.Extensions.Logging.RollingFile;

namespace Bing.Samples.Canal
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var configuration=new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"appsettings.json"))
                .AddEnvironmentVariables()
                .Build();
            var builder=new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddCanalService(x =>
                    {
                        x.RegisterSingleton<ConsoleHandler>();
                    });
                    services.AddLogging(factory =>
                    {
                        factory
                            .AddFilter("Microsoft", LogLevel.Debug)
                            .AddFilter("System", LogLevel.Information)
                            .AddFile(options =>
                            {
                                options.FileName = "diagnostics-";
                                options.LogDirectory = "LogFiles";
                                options.FileSizeLimit = 5 * 1024 * 1024;
                                options.FilesPerPeriodicityLimit = 10000;
                                options.Extension = "txt";
                                options.Periodicity = PeriodicityOptions.Hourly;
                                options.RetainedFileCountLimit = null;
                            })
                            .AddConsole();
                    });
                    services.AddSingleton<IConfiguration>(configuration);
                });
            builder.Build().Run();
        }
    }

    /// <summary>
    /// 控制台处理器
    /// </summary>
    public class ConsoleHandler : INotificationHandler<CanalBody>, IDisposable
    {
        private readonly ILogger<ConsoleHandler> _logger;
        public ConsoleHandler(ILogger<ConsoleHandler> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="notification">对象</param>
        public Task HandleAsync(CanalBody notification)
        {
            _logger.LogInformation($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] BatchId:{notification.BatchId}, Count:{notification.Message.Count}");
            foreach (var change in notification.Message)
            {
                _logger.LogInformation($"[{notification.BatchId}] DbName:{change.DbName},TbName:{change.TableName},EventType:{change.EventType}");
            }
            
            //Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] BatchId:{notification.BatchId}, Count:{notification.Message.Count}");
            //Console.WriteLine("DataChange:");
            //Console.WriteLine(JsonConvert.SerializeObject(notification.Message));
            return Task.CompletedTask;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Console.WriteLine($"{nameof(ConsoleHandler)} 已释放!");
        }
    }
}
