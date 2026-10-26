using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Service.FileStorage;
using Bing.Auditing.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Admin.Modules;

/// <summary>由宿主 ORM 提供的只读建表脚本生成器。</summary>
public sealed class AdminFileSchemaExporter
{
    private readonly Func<IServiceProvider, CancellationToken, Task<string>> _export;
    /// <summary>指定宿主使用的 ORM 脚本生成方法。</summary>
    public AdminFileSchemaExporter(Func<IServiceProvider, CancellationToken, Task<string>> export) => _export = export;
    /// <summary>生成脚本，不执行建表或迁移。</summary>
    public Task<string> ExportAsync(IServiceProvider services, CancellationToken ct) => _export(services, ct);
}

/// <summary>显式部署命令；只构造宿主，不启动 HTTP、CAP 或文件后台任务。</summary>
public static class AdminFileDeployment
{
    /// <summary>识别 files 命令并执行；普通启动参数返回 false。</summary>
    public static async Task<bool> TryRunAsync(string[] args, Func<string[], IHost> buildHost, CancellationToken ct = default)
    {
        if (args.Length == 0 || args[0] != "files") return false;
        if (args.Length < 2 || args[1] is not ("schema" or "audit-schema" or "initialize-permissions"))
            throw new ArgumentException("命令：files schema|audit-schema --output <UTF-8.sql>；files initialize-permissions --apply。");
        var command = args[1];
        var remaining = args.Skip(2).ToList();
        var apply = remaining.Remove("--apply");
        string output = null;
        var index = remaining.IndexOf("--output");
        if (index >= 0)
        {
            if (index + 1 >= remaining.Count || remaining[index + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("--output 缺少文件路径。");
            output = Path.GetFullPath(remaining[index + 1]);
            remaining.RemoveRange(index, 2);
        }
        if (command == "initialize-permissions" && (!apply || output != null)) throw new ArgumentException("权限初始化必须显式 --apply，且不接受 --output。");
        if (command != "initialize-permissions" && (apply || output == null)) throw new ArgumentException("schema 只导出文件，必须指定 --output，不接受 --apply。");
        if (output != null && File.Exists(output)) throw new IOException("输出文件已存在，请指定新路径。");

        using var host = buildHost(remaining.ToArray());
        using var scope = host.Services.CreateScope();
        if (command == "initialize-permissions")
        {
            await scope.ServiceProvider.GetRequiredService<IAdminFilePermissionResourceInitializer>().InitializeAsync(ct);
            Console.WriteLine("文件权限资源初始化完成；请在 Admin 中显式分配角色权限。");
        }
        else
        {
            var sql = command == "audit-schema" ? string.Join(";" + Environment.NewLine, AuditLogSchema.GetCreateStatements(AuditLogDatabase.MySql)) + ";" + Environment.NewLine
                : await scope.ServiceProvider.GetRequiredService<AdminFileSchemaExporter>().ExportAsync(scope.ServiceProvider, ct);
            await using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            ct.ThrowIfCancellationRequested();
            await writer.WriteAsync(sql.AsMemory(), ct);
            await writer.FlushAsync();
            Console.WriteLine("已导出建表 SQL。请确认目标数据库并完成现有库升级评估后执行。");
        }
        return true;
    }
}

/// <summary>在后台任务启动前激活指标监听器。</summary>
internal sealed class AdminFileMetricsStartup : IHostedService
{
    public AdminFileMetricsStartup(AdminFileMetricsExporter metrics) { }
    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
