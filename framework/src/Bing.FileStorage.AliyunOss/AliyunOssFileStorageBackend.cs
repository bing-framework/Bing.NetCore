using AlibabaCloud.OSS.V2;

namespace Bing.FileStorage;

/// <summary>
/// 持有命名 OSS 客户端的存储后端。
/// </summary>
/// <remarks>
/// 持有命名 OSS 客户端；范围门面共享客户端且不拥有其生命周期。
/// </remarks>
public sealed class AliyunOssFileStorageBackend : IFileStorageBackend, IDisposable
{
    /// <summary>
    /// 保存阿里云 OSS 后端使用的命名空间前缀。
    /// </summary>
    private readonly string _storageNamespace;
    /// <summary>
    /// 保存阿里云 OSS 客户端实例。
    /// </summary>
    private readonly Client _client;
    /// <summary>
    /// 指示后端是否拥有并可释放 OSS 客户端。
    /// </summary>
    private readonly bool _ownsClient;
    /// <summary>
    /// 保存列举分片时使用的分页大小。
    /// </summary>
    private readonly int _listPartsPageSize;
    /// <summary>
    /// 保存容器名称到 OSS 存储桶的映射。
    /// </summary>
    private readonly IReadOnlyDictionary<string, string> _containerBuckets;

    /// <summary>
    /// 初始化一个 <see cref="AliyunOssFileStorageBackend" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="options">用于初始化实例的配置选项。</param>
    /// <param name="client">用于访问对应后端或服务的客户端。</param>
    public AliyunOssFileStorageBackend(string name, BingAliyunOssStorageOptions options, Client client)
        : this(name, (options ?? throw new ArgumentNullException(nameof(options))).Snapshot(), client, ownsClient: false)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="AliyunOssFileStorageBackend" /> 类型的实例。
    /// </summary>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="options">用于初始化实例的配置选项。</param>
    /// <param name="client">用于访问对应后端或服务的客户端。</param>
    /// <param name="ownsClient">指示当前实例是否拥有客户端的释放责任。</param>
    internal AliyunOssFileStorageBackend(string name, BingAliyunOssStorageOptions options, Client client, bool ownsClient)
    {
        Name = FileStorageNames.ValidateIdentifier(name, nameof(name));
        _storageNamespace = FileStorageNames.ValidateIdentifier(options.StorageNamespace, nameof(options));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = ownsClient;
        _listPartsPageSize = options.ListPartsPageSize;
        _containerBuckets = options.ContainerBuckets.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IFileStorage Bind(StorageScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new AliyunOssFileStorage(Name, _storageNamespace, scope, _client, _listPartsPageSize, _containerBuckets);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }
}
