namespace Bing.EntityFrameworkCore.Migrations;

/// <summary>
/// 迁移服务
/// </summary>
public class MigrationService : IMigrationService
{
    /// <summary>
    /// 安装 dotnet-ef 全局工具，执行命令: dotnet tool install -g dotnet-ef
    /// </summary>
    public IMigrationService InstallEfTool()
    {
        throw new NotImplementedException();
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
