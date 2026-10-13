using Bing.DependencyInjection;

namespace Bing.EntityFrameworkCore.Migrations;

/// <summary>
/// 迁移服务
/// </summary>
public interface IMigrationService : ITransientDependency
{
    /// <summary>
    /// 安装全局 dotnet-ef 工具。
    /// </summary>
    /// <param name="version">要安装的工具版本；为空时使用最新版本。</param>
    /// <returns>当前迁移服务实例。</returns>
    /// <remarks>执行 <c>dotnet tool install -g dotnet-ef</c>；指定版本时追加 <c>--version</c> 参数。</remarks>
    IMigrationService InstallEfTool(string version = null);

    /// <summary>
    /// 卸载全局 dotnet-ef 工具。
    /// </summary>
    /// <returns>当前迁移服务实例。</returns>
    /// <remarks>执行 <c>dotnet tool uninstall -g dotnet-ef</c>。</remarks>
    IMigrationService UninstallEfTool();

    /// <summary>
    /// 更新全局 dotnet-ef 工具。
    /// </summary>
    /// <param name="version">要更新到的工具版本；为空时使用最新版本。</param>
    /// <returns>当前迁移服务实例。</returns>
    /// <remarks>执行 <c>dotnet tool update -g dotnet-ef</c>；指定版本时追加 <c>--version</c> 参数。</remarks>
    IMigrationService UpdateEfTool(string version = null);

    /// <summary>
    /// 创建指定名称的数据库迁移。
    /// </summary>
    /// <param name="migrationName">新迁移的名称。</param>
    /// <param name="dbContextRootPath">数据库上下文项目根目录的绝对路径。</param>
    /// <param name="isRemoveForeignKeys">是否从生成的迁移文件中移除外键。</param>
    /// <returns>当前迁移服务实例。</returns>
    /// <remarks>在指定项目目录中执行 <c>dotnet ef migrations add</c>。</remarks>
    IMigrationService AddMigration(string migrationName, string dbContextRootPath, bool isRemoveForeignKeys = false);

    /// <summary>
    /// 将待应用的迁移更新到数据库。
    /// </summary>
    /// <param name="dbContextRootPath">数据库上下文项目根目录的绝对路径。</param>
    /// <remarks>在指定项目目录中执行 <c>dotnet ef database update</c>。</remarks>
    void Migrate(string dbContextRootPath);
}
