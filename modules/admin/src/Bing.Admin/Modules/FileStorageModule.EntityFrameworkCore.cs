using System;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Modules;

public partial class FileStorageModule
{
    private static partial void ConfigureMetadata(IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("FileManagement") ?? configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("文件元数据连接未配置。");
        // 显式版本避免注册期间联网；部署时可按实际 MySQL 主版本配置。
        var version = Version.Parse(configuration["FileManagement:MySqlVersion"] ?? "8.0.36");
        services.AddBingFileManagementEntityFrameworkCore(options => options.UseMySql(connection, new MySqlServerVersion(version)));
        services.AddSingleton(new AdminFileSchemaExporter(async (provider, ct) =>
        {
            await using var db = await provider.GetRequiredService<IDbContextFactory<FileManagementDbContext>>().CreateDbContextAsync(ct);
            return db.Database.GenerateCreateScript();
        }));
    }
}
