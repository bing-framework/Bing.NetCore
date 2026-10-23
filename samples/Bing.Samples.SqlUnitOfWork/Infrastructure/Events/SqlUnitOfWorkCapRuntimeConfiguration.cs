using DotNetCore.CAP;

namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Events;

/// <summary>
/// 为 Sample 中每个隔离的 CAP runtime 配置同一 RabbitMQ endpoint 和本库 Outbox。
/// </summary>
public static class SqlUnitOfWorkCapRuntimeConfiguration
{
    public static void Configure(CapOptions options, string storageConnectionString,
        string rabbitMqConnectionString, string groupName)
    {
        options.UseMySql(storageConnectionString);
        var rabbit = new Uri(rabbitMqConnectionString);
        var credentials = rabbit.UserInfo.Split(':', 2);
        var virtualHost = Uri.UnescapeDataString(rabbit.AbsolutePath.TrimStart('/'));
        options.UseRabbitMQ(rabbitOptions =>
        {
            rabbitOptions.HostName = rabbit.Host;
            rabbitOptions.Port = rabbit.IsDefaultPort ? 5672 : rabbit.Port;
            rabbitOptions.UserName = Uri.UnescapeDataString(credentials.ElementAtOrDefault(0) ?? "guest");
            rabbitOptions.Password = Uri.UnescapeDataString(credentials.ElementAtOrDefault(1) ?? "guest");
            rabbitOptions.VirtualHost = string.IsNullOrEmpty(virtualHost) ? "/" : virtualHost;
        });
        options.DefaultGroupName = groupName;
        options.FailedRetryInterval = 2;
        options.FailedRetryCount = 5;
    }
}
