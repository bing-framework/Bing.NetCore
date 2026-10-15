using AspectCore.Extensions.Hosting;
using System;
using System.IO;
using Bing.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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
        public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

        /// <summary>
        /// 创建主机
        /// </summary>
        /// <param name="args">参数</param>
        public static IHostBuilder CreateWebHostBuilder(string[] args) => CreateHostBuilder(args);

        /// <summary>
        /// 创建按宿主角色选择 Web 或 Generic Host 的主机。
        /// </summary>
        /// <param name="args">命令行参数。</param>
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
