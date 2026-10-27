using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Bing.FileStorage;

/// <summary>仅用于启动配置，注册时复制；容器授权由上层业务处理。</summary>
public sealed class BingFileStorageOptions
{
    public string DefaultStore { get; set; } = "local-v1";
    public ISet<string> Containers { get; } = new HashSet<string>(StringComparer.Ordinal) { "attachments", "public" };
}

public static class FileStorageServiceCollectionExtensions
{
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

    /// <summary>注册单例后端；容器负责释放由工厂创建的 IDisposable 后端。</summary>
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

    private sealed class BackendName
    {
        public BackendName(string name) { Name = name; }
        public string Name { get; }
    }
}

/// <summary>只缓存固定后端，不缓存租户门面或请求服务。</summary>
internal sealed class FileStorageFactory : IFileStorageFactory
{
    private readonly IReadOnlyDictionary<string, IFileStorageBackend> _backends;
    private readonly HashSet<string> _containers;
    public FileStorageFactory(IEnumerable<IFileStorageBackend> backends, BingFileStorageOptions options)
    {
        _backends = backends.ToDictionary(x => x.Name, StringComparer.Ordinal);
        DefaultStore = options.DefaultStore;
        _containers = new HashSet<string>(options.Containers, StringComparer.Ordinal);
        if (!_backends.ContainsKey(DefaultStore)) throw new InvalidOperationException("默认文件存储未注册：" + DefaultStore);
    }
    public string DefaultStore { get; }
    public IFileStorage GetStorage(string storeName, StorageScope scope)
    {
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (!_backends.TryGetValue(storeName, out var backend)) throw new InvalidOperationException("文件存储未注册：" + storeName);
        return new ValidatedFileStorage(backend.Bind(scope), _containers);
    }
}

/// <summary>在所有能力入口执行同一容器校验。</summary>
internal sealed class ValidatedFileStorage : IFileStorage, IMultipartFileStorage, IMultipartFilePartReader, ISignedFileRequestProvider, IFileRangeReader
{
    private readonly IFileStorage _inner;
    private readonly ISet<string> _containers;
    public ValidatedFileStorage(IFileStorage inner, ISet<string> containers) { _inner = inner; _containers = containers; }
    public string Name => _inner.Name;
    public StorageScope Scope => _inner.Scope;
    public FileStorageCapabilities Capabilities => _inner.Capabilities;
    private void Validate(FileAddress address)
    {
        if (!_containers.Contains(address.Container)) throw new ArgumentException("容器未配置：" + address.Container);
    }
    private T Capability<T>() where T : class => _inner as T ?? throw new FileStorageException(FileStorageError.UnsupportedCapability, "后端不支持该能力。");
    public Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken = default)
    { Validate(request.Address); return _inner.SaveAsync(request, source, cancellationToken); }
    public Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default)
    { Validate(address); return _inner.ReadAsync(address, consume, cancellationToken); }
    public Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken cancellationToken = default)
    { Validate(address); return _inner.GetPropertiesAsync(address, cancellationToken); }
    public Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default)
    { Validate(address); return _inner.DeleteAsync(address, cancellationToken); }
    public Task<MultipartUpload> CreateMultipartAsync(FileWriteRequest request, int partSize, CancellationToken cancellationToken = default)
    { Validate(request.Address); return Capability<IMultipartFileStorage>().CreateMultipartAsync(request, partSize, cancellationToken); }
    public Task<FilePart> UploadPartAsync(MultipartUpload upload, int partNumber, Stream source, string? sha256 = null, CancellationToken cancellationToken = default)
    { Validate(upload.Request.Address); return Capability<IMultipartFileStorage>().UploadPartAsync(upload, partNumber, source, sha256, cancellationToken); }
    public Task<IReadOnlyList<FilePart>> ListPartsAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
    { Validate(upload.Request.Address); return Capability<IMultipartFileStorage>().ListPartsAsync(upload, cancellationToken); }
    public Task<FilePart?> GetPartAsync(MultipartUpload upload, int partNumber, CancellationToken cancellationToken = default)
    { Validate(upload.Request.Address); return Capability<IMultipartFileStorage>().GetPartAsync(upload, partNumber, cancellationToken); }
    public Task<FileWriteResult> CompleteMultipartAsync(MultipartUpload upload, IReadOnlyList<FilePart> parts, CancellationToken cancellationToken = default)
    { Validate(upload.Request.Address); return Capability<IMultipartFileStorage>().CompleteMultipartAsync(upload, parts, cancellationToken); }
    public Task AbortMultipartAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
    { Validate(upload.Request.Address); return Capability<IMultipartFileStorage>().AbortMultipartAsync(upload, cancellationToken); }
    public Task<SignedFileRequest> SignDownloadAsync(FileAddress address, TimeSpan lifetime, string? fileName = null, CancellationToken cancellationToken = default)
    { Validate(address); return Capability<ISignedFileRequestProvider>().SignDownloadAsync(address, lifetime, fileName, cancellationToken); }
    public Task<SignedFileRequest> SignPartAsync(MultipartUpload upload, int partNumber, string sha256, TimeSpan lifetime, CancellationToken cancellationToken = default)
    { Validate(upload.Request.Address); return Capability<ISignedFileRequestProvider>().SignPartAsync(upload, partNumber, sha256, lifetime, cancellationToken); }
    public Task ReadRangeAsync(FileAddress address, long offset, long length, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default)
    { Validate(address); return Capability<IFileRangeReader>().ReadRangeAsync(address, offset, length, consume, cancellationToken); }
}
