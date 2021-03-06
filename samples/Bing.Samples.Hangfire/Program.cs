﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using AspectCore.Extensions.Hosting;

namespace Bing.Samples.Hangfire
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateWebHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args).UseServiceContext().ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
    }
}
