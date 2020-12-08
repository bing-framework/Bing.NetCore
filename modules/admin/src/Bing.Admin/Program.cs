using AspectCore.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Bing.Admin
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreaetHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreaetHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
            .UseServiceContext().ConfigureWebHostDefaults(
                webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

    }
}
