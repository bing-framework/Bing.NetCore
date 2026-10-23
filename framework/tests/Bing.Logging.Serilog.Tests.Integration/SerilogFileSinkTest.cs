using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Xunit;

namespace Bing.Logging.Tests;

/// <summary>
/// 验证文件 sink 随测试 Host 释放。
/// </summary>
public sealed class SerilogFileSinkTest
{
    /// <summary>
    /// 文件 sink 应写入事件，并在 Host 释放后允许清理目录。
    /// </summary>
    [Fact]
    public async Task FileSink_ShouldWriteAndReleaseFileOnHostShutdown()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bing-serilog-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "log.txt");
        var marker = $"file-sink-{Guid.NewGuid():N}";
        Directory.CreateDirectory(directory);

        try
        {
            using (var host = new HostBuilder()
                       .ConfigureServices(services =>
                       {
                           services.AddLogging(logging => logging.ClearProviders());
                           services.AddSingleton<global::Serilog.ILogger>(_ => new LoggerConfiguration()
                               .MinimumLevel.Verbose()
                               .WriteTo.File(path, encoding: Encoding.UTF8)
                               .CreateLogger());
                           services.AddSingleton<ILoggerProvider>(serviceProvider =>
                               new SerilogLoggerProvider(serviceProvider.GetRequiredService<global::Serilog.ILogger>(),
                                   dispose: false));
                       })
                       .Build())
            {
                await host.StartAsync();
                host.Services.GetRequiredService<ILogger<SerilogFileSinkTest>>()
                    .LogInformation("Temporary file sink marker {Marker}", marker);
                await host.StopAsync();
            }

            Assert.True(File.Exists(path));
            Assert.Contains(marker, File.ReadAllText(path, Encoding.UTF8));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
