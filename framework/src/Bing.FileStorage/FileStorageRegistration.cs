using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Bing.FileStorage;

/// <summary>
/// 文件存储门面的启动配置。
/// </summary>
/// <remarks>仅用于启动配置，注册时复制；容器授权由上层业务处理。</remarks>
public sealed class BingFileStorageOptions
{
    /// <summary>
    /// 获取或设置默认使用的命名存储实例。
    /// </summary>
    public string DefaultStore { get; set; } = "local-v1";
    /// <summary>
    /// 获取允许使用的逻辑容器集合。
    /// </summary>
    public ISet<string> Containers { get; } = new HashSet<string>(StringComparer.Ordinal) { "attachments", "public" };
}

/// <summary>
/// 提供 Bing 文件存储服务和后端的依赖注入扩展。
/// </summary>
public static class FileStorageServiceCollectionExtensions
{
    /// <summary>
    /// 注册文件存储基础选项和文件存储工厂。
    /// </summary>
    /// <param name="services">接收文件存储服务的依赖注入集合。</param>
    /// <param name="configure">配置默认存储及允许容器的委托。</param>
    /// <returns>已注册文件存储选项及文件存储工厂的原服务集合。</returns>
    /// <remarks>
    /// 基础选项只能注册一次；配置会复制到注册快照，且至少需要一个容器。
    /// </remarks>
    public static IServiceCollection AddBingFileStorage(this IServiceCollection services, Action<BingFileStorageOptions>? configure = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (services.Any(x => x.ServiceType == typeof(BingFileStorageOptions)))
            throw new InvalidOperationException("文件存储基础配置只能注册一次。");
        var options = new BingFileStorageOptions();
        configure?.Invoke(options);
        FileStorageNames.ValidateIdentifier(options.DefaultStore, nameof(options.DefaultStore));
        foreach (var container in options.Containers) FileStorageNames.ValidateIdentifier(container, nameof(options.Containers));
        if (options.Containers.Count == 0) throw new ArgumentException("至少配置一个容器。");
        var snapshot = new BingFileStorageOptions { DefaultStore = options.DefaultStore };
        snapshot.Containers.Clear();
        foreach (var container in options.Containers) snapshot.Containers.Add(container);
        services.AddSingleton(snapshot);
        services.TryAddSingleton<IFileStorageFactory, FileStorageFactory>();
        return services;
    }

    /// <summary>
    /// 注册指定名称的文件存储后端工厂。
    /// </summary>
    /// <param name="services">要添加文件存储后端的依赖注入服务集合。</param>
    /// <param name="name">文件存储后端名称。</param>
    /// <param name="factory">创建文件存储后端的工厂委托。</param>
    /// <returns>原依赖注入服务集合。</returns>
    /// <remarks>
    /// 后端名称无效或名称已注册时抛出异常。
    /// </remarks>
    public static IServiceCollection AddBingFileStorageBackend(this IServiceCollection services, string name, Func<IServiceProvider, IFileStorageBackend> factory)
    {
        FileStorageNames.ValidateIdentifier(name, nameof(name));
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        if (services.Any(x => x.ImplementationInstance is BackendName n && n.Name == name))
            throw new InvalidOperationException("存储实例重复：" + name);
        services.AddSingleton(new BackendName(name));
        services.AddSingleton(factory);
        return services;
    }

    /// <summary>
    /// 命名文件存储后端的注册标识。
    /// </summary>
    private sealed class BackendName
    {
        /// <summary>
        /// 初始化一个 <see cref="BackendName" /> 类型的实例。
        /// </summary>
        /// <param name="name">实例使用的名称。</param>
        public BackendName(string name) { Name = name; }
        /// <summary>
        /// 获取已注册文件存储后端的名称。
        /// </summary>
        public string Name { get; }
    }
}

/// <summary>
/// 绑定命名后端和操作范围的文件存储工厂。
/// </summary>
/// <remarks>
/// 只缓存固定后端，不缓存租户门面或请求服务。
/// </remarks>
internal sealed class FileStorageFactory : IFileStorageFactory
{
    /// <summary>
    /// 保存按名称索引的文件存储后端。
    /// </summary>
    private readonly IReadOnlyDictionary<string, IFileStorageBackend> _backends;
    /// <summary>
    /// 保存已注册的容器名称集合。
    /// </summary>
    private readonly HashSet<string> _containers;
    /// <summary>
    /// 保存用于按名称解析后端的解析器列表。
    /// </summary>
    private readonly IReadOnlyList<IFileStorageBackendResolver> _resolvers;
    /// <summary>
    /// 初始化一个 <see cref="FileStorageFactory" /> 类型的实例。
    /// </summary>
    /// <param name="backends">已注册的后端集合。</param>
    /// <param name="options">用于初始化实例的配置选项。</param>
    /// <param name="resolvers">用于解析文件存储后端的解析器集合。</param>
    public FileStorageFactory(IEnumerable<IFileStorageBackend> backends, BingFileStorageOptions options,
        IEnumerable<IFileStorageBackendResolver> resolvers)
    {
        _backends = backends.ToDictionary(x => x.Name, StringComparer.Ordinal);
        DefaultStore = options.DefaultStore;
        _containers = new HashSet<string>(options.Containers, StringComparer.Ordinal);
        _resolvers = resolvers.ToArray();
        if (!_backends.ContainsKey(DefaultStore)) throw new InvalidOperationException("默认文件存储未注册：" + DefaultStore);
    }
    /// <inheritdoc />
    public string DefaultStore { get; }
    /// <inheritdoc />
    public IFileStorage GetStorage(string storeName, StorageScope scope)
    {
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        FileStorageNames.ValidateIdentifier(storeName, nameof(storeName));
        if (!_backends.TryGetValue(storeName, out var backend))
        {
            foreach (var resolver in _resolvers)
            {
                var candidate = resolver.Resolve(storeName);
                if (candidate == null) continue;
                if (backend != null) throw new InvalidOperationException("多个解析器匹配存储实例：" + storeName);
                if (candidate.Name != storeName) throw new InvalidOperationException("解析器返回了不同的存储实例。");
                backend = candidate;
            }
        }
        if (backend == null) throw new InvalidOperationException("文件存储未注册：" + storeName);
        return new ValidatedFileStorage(backend.Bind(scope), _containers);
    }
}

/// <summary>
/// 统一校验容器范围的文件存储包装器。
/// </summary>
/// <remarks>
/// 在所有能力入口执行同一容器校验。
/// </remarks>
internal sealed class ValidatedFileStorage : IFileStorage, IMultipartFileStorage, IMultipartFilePartReader, ISignedFileRequestProvider, IFileRangeReader, IFileStorageLimitProvider
{
    /// <summary>
    /// 保存受校验包装的内部文件存储。
    /// </summary>
    private readonly IFileStorage _inner;
    /// <summary>
    /// 保存允许访问的容器名称集合。
    /// </summary>
    private readonly ISet<string> _containers;
    /// <summary>
    /// 初始化一个 <see cref="ValidatedFileStorage" /> 类型的实例。
    /// </summary>
    /// <param name="inner">由当前包装器保留或转发的内部对象或异常。</param>
    /// <param name="containers">允许访问的逻辑容器名称集合。</param>
    public ValidatedFileStorage(IFileStorage inner, ISet<string> containers) { _inner = inner; _containers = containers; }
    /// <inheritdoc />
    public string Name => _inner.Name;
    /// <inheritdoc />
    public StorageScope Scope => _inner.Scope;
    /// <inheritdoc />
    public FileStorageCapabilities Capabilities => _inner.Capabilities;
    /// <inheritdoc />
    public Task<FileStorageLimits?> GetLimitsAsync(CancellationToken cancellationToken = default)
        => _inner is IFileStorageLimitProvider limits ? limits.GetLimitsAsync(cancellationToken) : Task.FromResult<FileStorageLimits?>(null);
    /// <summary>
    /// 验证文件地址所用的容器已登记。
    /// </summary>
    /// <param name="address">待验证的文件地址。</param>
    /// <remarks>
    /// 容器未配置时抛出 ArgumentException。
    /// </remarks>
    private void Validate(FileAddress address)
    {
        if (!_containers.Contains(address.Container)) throw new ArgumentException("容器未配置：" + address.Container);
    }
    /// <summary>
    /// 获取后端实现的指定存储能力。
    /// </summary>
    /// <typeparam name="T">要获取的可选文件存储能力接口。</typeparam>
    /// <returns>所请求的文件存储能力实现。</returns>
    /// <remarks>
    /// 后端未实现该能力时抛出 UnsupportedCapability 文件存储异常。
    /// </remarks>
    private T Capability<T>() where T : class => _inner as T ?? throw new FileStorageException(FileStorageError.UnsupportedCapability, "后端不支持该能力。");
    /// <inheritdoc />
    public Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken = default)
    { Validate(request.Address); return _inner.SaveAsync(request, source, cancellationToken); }
    /// <inheritdoc />
    public Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default)
    { Validate(address); return _inner.ReadAsync(address, consume, cancellationToken); }
    /// <inheritdoc />
    public Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken cancellationToken = default)
    { Validate(address); return _inner.GetPropertiesAsync(address, cancellationToken); }
    /// <inheritdoc />
    public Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default)
    { Validate(address); return _inner.DeleteAsync(address, cancellationToken); }
    /// <inheritdoc />
    public Task<MultipartUpload> CreateMultipartAsync(FileWriteRequest request, int partSize, CancellationToken cancellationToken = default)
    { Validate(request.Address); return Capability<IMultipartFileStorage>().CreateMultipartAsync(request, partSize, cancellationToken); }
    /// <inheritdoc />
    public Task<FilePart> UploadPartAsync(MultipartUpload upload, int partNumber, Stream source, string? sha256 = null, CancellationToken cancellationToken = default)
    { Validate(upload.Request.Address); return Capability<IMultipartFileStorage>().UploadPartAsync(upload, partNumber, source, sha256, cancellationToken); }
    /// <inheritdoc />
    public Task<IReadOnlyList<FilePart>> ListPartsAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
    { Validate(upload.Request.Address); return Capability<IMultipartFileStorage>().ListPartsAsync(upload, cancellationToken); }
    /// <inheritdoc />
    public Task<FilePart?> GetPartAsync(MultipartUpload upload, int partNumber, CancellationToken cancellationToken = default)
    { Validate(upload.Request.Address); return Capability<IMultipartFileStorage>().GetPartAsync(upload, partNumber, cancellationToken); }
    /// <inheritdoc />
    public Task<FileWriteResult> CompleteMultipartAsync(MultipartUpload upload, IReadOnlyList<FilePart> parts, CancellationToken cancellationToken = default)
    { Validate(upload.Request.Address); return Capability<IMultipartFileStorage>().CompleteMultipartAsync(upload, parts, cancellationToken); }
    /// <inheritdoc />
    public Task AbortMultipartAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
    { Validate(upload.Request.Address); return Capability<IMultipartFileStorage>().AbortMultipartAsync(upload, cancellationToken); }
    /// <inheritdoc />
    public Task<SignedFileRequest> SignDownloadAsync(FileAddress address, TimeSpan lifetime, string? fileName = null, CancellationToken cancellationToken = default)
    { Validate(address); return Capability<ISignedFileRequestProvider>().SignDownloadAsync(address, lifetime, fileName, cancellationToken); }
    /// <inheritdoc />
    public Task<SignedFileRequest> SignPartAsync(MultipartUpload upload, int partNumber, string sha256, TimeSpan lifetime, CancellationToken cancellationToken = default)
    { Validate(upload.Request.Address); return Capability<ISignedFileRequestProvider>().SignPartAsync(upload, partNumber, sha256, lifetime, cancellationToken); }
    /// <inheritdoc />
    public Task ReadRangeAsync(FileAddress address, long offset, long length, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default)
    { Validate(address); return Capability<IFileRangeReader>().ReadRangeAsync(address, offset, length, consume, cancellationToken); }
}
