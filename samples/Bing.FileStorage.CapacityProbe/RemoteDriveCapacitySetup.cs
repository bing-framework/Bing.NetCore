using Bing.Biz.FileManagement.AspNetCore;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.Biz.FileManagement.FreeSql;
using Bing.FileStorage.AliyunDrive;
using Bing.FileStorage.Pan115;
using Bing.FileStorage.RemoteDrive;
using Bing.FileStorage.FileSystem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage.CapacityProbe;

/// <summary>使用专用测试数据库里已授权的连接；不把令牌写到命令行或容量证据。</summary>
internal static class RemoteDriveCapacitySetup
{
    public static void Register(IServiceCollection services, Dictionary<string, string> arguments, string runRoot, string provider)
    {
        var config = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(arguments["--config"])).AddEnvironmentVariables().Build();
        var section = config.GetSection("FileManagement");
        var connection = config.GetConnectionString("FileManagement") ?? throw new ArgumentException("需要专用测试元数据库。");
        if (!string.Equals(section["Database"], "Sqlite", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("网盘容量探针只使用专用 SQLite 元数据库。");
        services.AddLogging();
        if (section["Orm"] == "FreeSql")
        {
            services.AddSingleton<IFreeSql>(_ => new global::FreeSql.FreeSqlBuilder().UseConnectionString(global::FreeSql.DataType.Sqlite, connection).UseAutoSyncStructure(false).Build());
            services.AddBingFileManagementFreeSql(sp => sp.GetRequiredService<IFreeSql>());
        }
        else services.AddBingFileManagementEntityFrameworkCore(o => o.UseSqlite(connection));
        services.AddBingRemoteDriveCredentialProtection(section["KeyRingPath"] ?? throw new ArgumentException("需要原授权连接的持久密钥环。"), section["KeyRingApplicationName"] ?? "Bing.FileManagement");
        services.AddBingFileSystemStorage("capacity-stage", o => o.RootPath = runRoot);
        foreach (var template in section.GetSection("RemoteDrives").GetChildren())
            if (provider == "aliyundrive" && template["Provider"] == "AliyunDrive")
                services.AddBingAliyunDriveStorage(template.Key, o => { template.Bind(o); o.StagingStoreName = "capacity-stage"; o.StagingContainer = "attachments"; });
            else if (provider == "pan115" && template["Provider"] == "Pan115")
                services.AddBingPan115Storage(template.Key, o => { template.Bind(o); o.StagingStoreName = "capacity-stage"; o.StagingContainer = "attachments"; });
    }
    public static async Task<IFileStorage> ResolveAsync(IServiceProvider services, Dictionary<string, string> arguments, string provider)
    {
        await using var scope = services.CreateAsyncScope();
        var connection = await scope.ServiceProvider.GetRequiredService<IRemoteDriveStateStore>().FindConnectionAsync(Guid.Parse(arguments["--connection"]));
        if (connection == null || connection.State != RemoteDriveConnectionState.Active || connection.Provider != provider)
            throw new ArgumentException("必须使用已授权且匹配后端的专用测试连接。");
        return services.GetRequiredService<IFileStorageFactory>().GetStorage(connection.StoreName, connection.Scope());
    }
}
