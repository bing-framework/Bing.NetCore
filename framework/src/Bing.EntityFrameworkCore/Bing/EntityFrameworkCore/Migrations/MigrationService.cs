using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.EntityFrameworkCore.Migrations;

/// <summary>
/// 迁移服务
/// </summary>
public class MigrationService : IMigrationService
{
    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger<MigrationService> _logger;

    /// <summary>
    /// 迁移文件服务
    /// </summary>
    private readonly IMigrationFileService _migrationFileService;

    /// <summary>
    /// 初始化一个<see cref="MigrationService"/>类型的实例
    /// </summary>
    /// <param name="logger">日志</param>
    /// <param name="migrationFileService">迁移文件服务</param>
    /// <exception cref="ArgumentNullException"></exception>
    public MigrationService(ILogger<MigrationService> logger, IMigrationFileService migrationFileService)
    {
        _logger = logger ?? NullLogger<MigrationService>.Instance;
        _migrationFileService = migrationFileService ?? throw new ArgumentNullException(nameof(migrationFileService));
    }

    /// <summary>
    /// 安装 dotnet-ef 全局工具，执行命令: dotnet tool install -g dotnet-ef
    /// </summary>
    public IMigrationService InstallEfTool()
    {
        _logger.LogTrace("准备安装 dotnet-ef 全局工具.");
        return this;
    }

    /// <summary>
    /// 更新 dotnet-ef 全局工具，执行命令: dotnet tool update -g dotnet-ef
    /// </summary>
    public IMigrationService UpdateEfTool()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 添加迁移，执行命令：dotnet ef migrations add migrationName
    /// </summary>
    /// <param name="migrationName">迁移名称</param>
    /// <param name="dbContextRootPath">数据上下文项目根目录绝对路径。范例：D:\\Test\\src\\Test.Data.SqlServer</param>
    /// <param name="isRemoveForeignKeys">是否移除迁移文件中的所有外键</param>
    public IMigrationService AddMigration(string migrationName, string dbContextRootPath, bool isRemoveForeignKeys = false)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 执行迁移，执行命令: dotnet ef database update
    /// </summary>
    /// <param name="dbContextRootPath">数据上下文项目根目录绝对路径。范例：D:\\Test\\src\\Test.Data.SqlServer</param>
    public void Migrate(string dbContextRootPath)
    {
        throw new NotImplementedException();
    }
}
