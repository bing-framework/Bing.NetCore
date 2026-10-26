using AspectCore.Extensions.Hosting;
using System;
using Bing.Admin.Modules;
using System.IO;
using Bing.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin
{
    /// <summary>
    /// 程序入口点
    /// </summary>
    public class Program
    {
        /// <summary>
        /// 主函数
        /// </summary>
        /// <param name="args">参数</param>
        public static void Main(string[] args)
        {
            Console.InputEncoding = new System.Text.UTF8Encoding(false);
            Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            if (Modules.AdminFileDeployment.TryRunAsync(args, CreateFileDeploymentHost).GetAwaiter().GetResult()) return;
            CreateHostBuilder(args).Build().Run();
        }

        /// <summary>部署命令只装配 ORM、映射与文件服务，不创建 Redis、消息消费者或 Web 管道。</summary>
        public static IHost CreateFileDeploymentHost(string[] args) => Host.CreateDefaultBuilder(args)
            .UseServiceContext().ConfigureServices((context, services) =>
            {
                services.ReplaceConfiguration(context.Configuration);
                services.AddBing().AddModule<MapperModule>().AddModule<EntityFrameworkCoreModule>().AddModule<FileStorageModule>();
            }).Build();

        /// <summary>
        /// 创建主机
        /// </summary>
        /// <param name="args">参数</param>
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args)
                .UseServiceContext();
            if (IsBingBackgroundRole(args))
            {
                return builder.ConfigureServices((context, services) =>
                    new Startup(context.Configuration).ConfigureServices(services));
            }
            return builder.ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
        }

        /// <summary>
        /// 根据宿主配置判断是否使用无 Web 管道的 Generic Host。
        /// </summary>
        /// <param name="args">命令行参数。</param>
        /// <returns>后台角色返回 <see langword="true"/>。</returns>
        private static bool IsBingBackgroundRole(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                               ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                               ?? Environments.Production;
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .AddCommandLine(args ?? Array.Empty<string>())
                .Build();
            return configuration.IsBingBackgroundRole();
        }
    }
}
