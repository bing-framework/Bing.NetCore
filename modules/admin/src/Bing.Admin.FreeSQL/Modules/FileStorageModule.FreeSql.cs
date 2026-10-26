using System;
using Bing.Biz.FileManagement.FreeSql;
using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Modules;

public partial class FileStorageModule
{
    private static partial void ConfigureMetadata(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("FileManagement")
            ?? configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("文件元数据连接未配置。");

        services.AddSingleton<IFreeSql>(_ => new FreeSqlBuilder()
            .UseConnectionString(DataType.MySql, connection)
            .UseLazyLoading(false)
            .UseAutoSyncStructure(false)
            .Build());
        services.AddBingFileManagementFreeSql(provider => provider.GetRequiredService<IFreeSql>());
        services.AddSingleton(new AdminFileSchemaExporter((provider, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return System.Threading.Tasks.Task.FromResult(FileManagementFreeSqlSchema.GenerateScript(provider.GetRequiredService<IFreeSql>()));
        }));
    }
}
