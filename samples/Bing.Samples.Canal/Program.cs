using System;
using System.IO;
using System.Threading.Tasks;
using Bing.Canal.Server;
using Bing.Canal.Server.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
                            .AddConsole();
                    });
                    services.AddSingleton<IConfiguration>(configuration);
                });
            builder.Build().Run();
        }
    }

    public class ConsoleHandler : INotificationHandler<CanalBody>, IDisposable
    {
        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="notification">对象</param>
        public Task HandleAsync(CanalBody notification)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] BatchId:{notification.BatchId}, Count:{notification.Message.Count}");
            //Console.WriteLine("DataChange:");
            //Console.WriteLine(JsonConvert.SerializeObject(notification.Message));
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
