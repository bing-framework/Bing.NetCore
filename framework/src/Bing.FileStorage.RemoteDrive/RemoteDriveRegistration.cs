using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.FileStorage.RemoteDrive;

/// <summary>
/// 网盘模板的传输与中转配置。
/// </summary>
/// <remarks>注册后复制的网盘模板。上限必须来自账号已确认能力。</remarks>
public class RemoteDriveOptions
{
    /// <summary>
    /// 获取或设置中转文件使用的命名存储实例。
    /// </summary>
    /// <remarks>默认值为 <c>&quot;local-v1&quot;</c>。</remarks>
    public string StagingStoreName { get; set; } = "local-v1";
    /// <summary>
    /// 获取或设置中转文件使用的逻辑容器。
    /// </summary>
    /// <remarks>默认值为 <c>&quot;attachments&quot;</c>。</remarks>
    public string StagingContainer { get; set; } = "attachments";
    /// <summary>
    /// 获取或设置单个文件允许的最大字节数。
    /// </summary>
    /// <remarks>必须由宿主填写已确认的账号容量上限，并且大于 0。</remarks>
    public long MaximumFileLength { get; set; }
    /// <summary>
    /// 获取或设置单次中转磁盘预算的字节数。
    /// </summary>
    /// <remarks>单次中转磁盘预算，按分片与合并内容共存预留两份；并发总量由宿主资源门控限制。默认值为 214748364800。</remarks>
    public long StagingBudgetBytes { get; set; } = 200L * 1024 * 1024 * 1024;
    /// <summary>
    /// 获取或设置允许操作的网盘根目录标识。
    /// </summary>
    /// <remarks>默认值为 <c>&quot;root&quot;</c>。</remarks>
    public string RootFolderId { get; set; } = "root";
    /// <summary>
    /// 获取或设置传输任务的最长有效时长。
    /// </summary>
    /// <remarks>必须大于零，且不得超过 7 天。默认值为 7 天。</remarks>
    public TimeSpan TransferLifetime { get; set; } = TimeSpan.FromDays(7);
    /// <summary>
    /// 获取或设置单次处理租约的有效时长。
    /// </summary>
    /// <remarks>必须在 30 秒至 10 分钟之间。默认值为 2 分钟。</remarks>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);
    /// <summary>
    /// 获取或设置连接/响应头及数据传输无进展期限。
    /// </summary>
    /// <remarks>注册时形成快照。</remarks>
    public RemoteDriveTransportOptions TransportTimeouts { get; set; } = new();
}

/// <summary>
/// 注册后固定的网盘模板配置。
/// </summary>
public sealed class RemoteDriveTemplate : IDisposable
{
    /// <summary>
    /// 指示远程盘模板是否拥有并释放客户端。
    /// </summary>
    private readonly bool _ownsClient;
    /// <summary>
    /// 初始化一个 <see cref="RemoteDriveTemplate" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="options">用于初始化实例的配置选项。</param>
    /// <param name="client">用于访问对应后端或服务的客户端。</param>
    /// <param name="ownsClient">指示当前实例是否拥有客户端的释放责任。</param>
    public RemoteDriveTemplate(string name, RemoteDriveOptions options, IRemoteDriveClient client, bool ownsClient = false)
    {
        FileStorageNames.ValidateIdentifier(name, nameof(name));
        if (name.Length > 29) throw new ArgumentException("网盘模板名不能超过 29 字符。");
        FileStorageNames.ValidateIdentifier(options.StagingStoreName, nameof(options.StagingStoreName));
        FileStorageNames.ValidateIdentifier(options.StagingContainer, nameof(options.StagingContainer));
        if (options.MaximumFileLength <= 0 || options.StagingBudgetBytes < 2 || options.TransferLifetime <= TimeSpan.Zero || options.TransferLifetime > TimeSpan.FromDays(7)
            || options.LeaseDuration < TimeSpan.FromSeconds(30) || options.LeaseDuration > TimeSpan.FromMinutes(10)
            || string.IsNullOrWhiteSpace(options.RootFolderId)) throw new ArgumentException("必须配置已确认的账号上限、根目录和有效期限。");
        Name = name; Client = client; StagingStoreName = options.StagingStoreName; StagingContainer = options.StagingContainer;
        _ownsClient = ownsClient;
        MaximumFileLength = Math.Min(options.MaximumFileLength, options.StagingBudgetBytes / 2); RootFolderId = options.RootFolderId;
        TransferLifetime = options.TransferLifetime; LeaseDuration = options.LeaseDuration;
        TransportTimeouts = options.TransportTimeouts.Snapshot();
    }
    /// <summary>
    /// 获取宿主登记的模板名称。
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// 获取模板绑定的网盘客户端。
    /// </summary>
    public IRemoteDriveClient Client { get; }
    /// <summary>
    /// 获取中转文件使用的命名存储实例。
    /// </summary>
    public string StagingStoreName { get; }
    /// <summary>
    /// 获取中转文件使用的逻辑容器。
    /// </summary>
    public string StagingContainer { get; }
    /// <summary>
    /// 获取允许操作的网盘根目录标识。
    /// </summary>
    public string RootFolderId { get; }
    /// <summary>
    /// 获取单个文件允许的最大字节数。
    /// </summary>
    /// <remarks>已取账号文件上限与中转磁盘预算一半中的较小值。</remarks>
    public long MaximumFileLength { get; }
    /// <summary>
    /// 获取传输任务的最长有效时长。
    /// </summary>
    public TimeSpan TransferLifetime { get; }
    /// <summary>
    /// 获取单次处理租约的有效时长。
    /// </summary>
    public TimeSpan LeaseDuration { get; }
    /// <summary>
    /// 获取网盘传输的响应头与空闲超时配置。
    /// </summary>
    internal RemoteDriveTransportOptions TransportTimeouts { get; }
    /// <summary>
    /// 根据连接标识生成专属存储名称。
    /// </summary>
    /// <param name="connectionId">远程存储连接标识。</param>
    /// <returns>由模板名称和 N 格式连接 Guid 组成的存储名称。</returns>
    public string StoreName(Guid connectionId) => Name + "-c-" + connectionId.ToString("N");
    /// <inheritdoc />
    /// <remarks>
    /// <para>只释放注册入口创建的客户端；调用方传入的客户端默认由调用方管理。</para>
    /// </remarks>
    public void Dispose() { if (_ownsClient && Client is IDisposable disposable) disposable.Dispose(); }
}

/// <summary>
/// 提供 RemoteDrive 文件存储后端的依赖注入扩展。
/// </summary>
public static class RemoteDriveRegistration
{
    /// <summary>
    /// 命名网盘模板的注册标识。
    /// </summary>
    /// <param name="Name">宿主登记的网盘模板名称。</param>
    private sealed record TemplateName(string Name);
    /// <summary>
    /// 注册远程网盘存储模板及相关服务。
    /// </summary>
    /// <param name="services">要添加远程网盘服务的依赖注入服务集合。</param>
    /// <param name="template">要注册的远程网盘模板。</param>
    /// <returns>原依赖注入服务集合。</returns>
    /// <remarks>
    /// 已存在同名模板时抛出参数异常。
    /// </remarks>
    public static IServiceCollection AddBingRemoteDrive(this IServiceCollection services, RemoteDriveTemplate template)
    {
        if (services.Any(x => x.ImplementationInstance is TemplateName t && t.Name == template.Name))
            throw new ArgumentException("网盘模板重复。");
        services.AddSingleton(new TemplateName(template.Name));
        services.AddSingleton(_ => template);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFileStorageBackendResolver, RemoteDriveResolver>());
        services.TryAddScoped<IRemoteDriveCredentialProvider, RemoteDriveCredentialProvider>();
        services.TryAddScoped<RemoteDriveMaintenance>();
        services.TryAddSingleton(_ => new RemoteDriveTransport());
        return services;
    }
}

/// <summary>
/// 解析已注册的 RemoteDrive 文件存储后端。
/// </summary>
internal sealed class RemoteDriveResolver : IFileStorageBackendResolver
{
    /// <summary>
    /// 保存远程盘解析器使用的模板数组。
    /// </summary>
    private readonly RemoteDriveTemplate[] _templates;
    /// <summary>
    /// 保存远程盘解析器使用的服务提供程序。
    /// </summary>
    private readonly IServiceProvider _services;
    /// <summary>
    /// 初始化一个 <see cref="RemoteDriveResolver" /> 类型的实例。
    /// </summary>
    /// <param name="templates">用于配置远端存储连接的模板集合。</param>
    /// <param name="services">用于注册或解析实例依赖项的服务集合。</param>
    public RemoteDriveResolver(IEnumerable<RemoteDriveTemplate> templates, IServiceProvider services)
    { _templates = templates.ToArray(); _services = services; }
    /// <inheritdoc />
    public IFileStorageBackend? Resolve(string storeName)
    {
        foreach (var template in _templates)
        {
            var prefix = template.Name + "-c-";
            if (storeName.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParseExact(storeName[prefix.Length..], "N", out var id))
                return new RemoteDriveBackend(storeName, id, template, _services);
        }
        return null;
    }
}

/// <summary>
/// 绑定 RemoteDrive 文件存储后端配置并提供存储实例。
/// </summary>
internal sealed class RemoteDriveBackend : IFileStorageBackend
{
    /// <summary>
    /// 保存远程盘后端关联的连接标识。
    /// </summary>
    private readonly Guid _connectionId;
    /// <summary>
    /// 保存远程盘后端使用的模板。
    /// </summary>
    private readonly RemoteDriveTemplate _template;
    /// <summary>
    /// 保存远程盘后端使用的服务提供程序。
    /// </summary>
    private readonly IServiceProvider _services;
    /// <summary>
    /// 初始化一个 <see cref="RemoteDriveBackend" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="connectionId">数据库连接标识。</param>
    /// <param name="template">用于配置远端存储连接或后端的模板。</param>
    /// <param name="services">用于注册或解析实例依赖项的服务集合。</param>
    public RemoteDriveBackend(string name, Guid connectionId, RemoteDriveTemplate template, IServiceProvider services)
    { Name = name; _connectionId = connectionId; _template = template; _services = services; }
    /// <inheritdoc />
    public string Name { get; }
    /// <inheritdoc />
    public IFileStorage Bind(StorageScope scope) => new RemoteDriveStorage(Name, scope, _connectionId, _template, _services);
}
