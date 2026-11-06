using AlibabaCloud.OSS.V2;
using AlibabaCloud.OSS.V2.Credentials;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage;

/// <summary>
/// 阿里云 OSS 命名存储实例配置。
/// </summary>
public sealed class BingAliyunOssStorageOptions
{
    /// <summary>
    /// 获取或设置OSS 服务区域。
    /// </summary>
    /// <remarks>默认值为 <c>&quot;cn-hangzhou&quot;</c>。</remarks>
    public string Region { get; set; } = "cn-hangzhou";
    /// <summary>
    /// 获取或设置OSS 服务端点。
    /// </summary>
    public string? Endpoint { get; set; }
    /// <summary>
    /// 获取或设置存储服务访问密钥标识。
    /// </summary>
    public string? AccessKeyId { get; set; }
    /// <summary>
    /// 获取或设置存储服务访问密钥。
    /// </summary>
    public string? AccessKeySecret { get; set; }
    /// <summary>
    /// 获取或设置临时访问凭据的安全令牌。
    /// </summary>
    public string? SecurityToken { get; set; }
    /// <summary>
    /// 获取或设置隔离当前应用对象键的命名空间。
    /// </summary>
    /// <remarks>默认值为 <c>&quot;bing&quot;</c>。</remarks>
    public string StorageNamespace { get; set; } = "bing";
    /// <summary>
    /// 获取或设置是否使用路径式桶寻址。
    /// </summary>
    /// <remarks>默认值为 false。</remarks>
    public bool UsePathStyle { get; set; }
    /// <summary>
    /// 获取或设置查询分片清单时的单页大小。
    /// </summary>
    /// <remarks>默认值为 1000。</remarks>
    public int ListPartsPageSize { get; set; } = 1000;
    /// <summary>
    /// 获取或设置逻辑容器到实际存储桶的映射。
    /// </summary>
    public Dictionary<string, string> ContainerBuckets { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 复制并验证 Aliyun OSS 存储选项。
    /// </summary>
    /// <returns>包含已验证配置的新选项实例。</returns>
    /// <remarks>
    /// 验证区域、命名空间、分页大小、访问凭据、Endpoint 和逻辑容器映射。
    /// </remarks>
    internal BingAliyunOssStorageOptions Snapshot()
    {
        var copy = new BingAliyunOssStorageOptions
        {
            Region = Region,
            Endpoint = Endpoint,
            AccessKeyId = AccessKeyId,
            AccessKeySecret = AccessKeySecret,
            SecurityToken = SecurityToken,
            StorageNamespace = StorageNamespace,
            UsePathStyle = UsePathStyle,
            ListPartsPageSize = ListPartsPageSize,
            ContainerBuckets = new Dictionary<string, string>(ContainerBuckets ?? throw new ArgumentNullException(nameof(ContainerBuckets)), StringComparer.Ordinal)
        };

        if (string.IsNullOrWhiteSpace(copy.Region))
            throw new ArgumentException("OSS 区域不能为空。", nameof(Region));
        FileStorageNames.ValidateIdentifier(copy.StorageNamespace, nameof(StorageNamespace));
        if (copy.ListPartsPageSize is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(ListPartsPageSize), "分片分页大小须为 1 至 1000。");
        if (string.IsNullOrWhiteSpace(copy.AccessKeyId) != string.IsNullOrWhiteSpace(copy.AccessKeySecret))
            throw new ArgumentException("AccessKeyId 与 AccessKeySecret 必须同时配置。");
        if (string.IsNullOrWhiteSpace(copy.AccessKeyId))
        {
            copy.AccessKeyId = null;
            copy.AccessKeySecret = null;
        }
        if (!string.IsNullOrWhiteSpace(copy.SecurityToken) && string.IsNullOrWhiteSpace(copy.AccessKeyId))
            throw new ArgumentException("配置 SecurityToken 时必须同时配置访问密钥。");
        if (copy.Endpoint != null &&
            (!Uri.TryCreate(copy.Endpoint, UriKind.Absolute, out var endpoint) ||
             (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps)))
            throw new ArgumentException("OSS Endpoint 必须是绝对 HTTP 或 HTTPS 地址。", nameof(Endpoint));

        foreach (var pair in copy.ContainerBuckets)
        {
            FileStorageNames.ValidateIdentifier(pair.Key, nameof(ContainerBuckets));
            AliyunOssFileStorage.ValidateBucket(pair.Value);
        }

        return copy;
    }
}

/// <summary>
/// OSS 文件存储服务注册扩展。
/// </summary>
/// <remarks>
/// 注册一个共享 OSS 客户端和按范围绑定的存储门面。
/// </remarks>
public static class BingAliyunOssStorageExtensions
{
    /// <summary>
    /// 配置并注册 Aliyun OSS 文件存储后端。
    /// </summary>
    /// <param name="services">要添加后端的依赖注入服务集合。</param>
    /// <param name="name">后端名称。</param>
    /// <param name="configure">配置 OSS 存储选项的委托。</param>
    /// <returns>原依赖注入服务集合。</returns>
    /// <remarks>
    /// 配置完成后复制并验证选项，再注册后端工厂。
    /// </remarks>
    public static IServiceCollection AddBingAliyunOssStorage(
        this IServiceCollection services,
        string name,
        Action<BingAliyunOssStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        FileStorageNames.ValidateIdentifier(name, nameof(name));

        var options = new BingAliyunOssStorageOptions();
        configure(options);
        var snapshot = options.Snapshot();
        services.AddBingFileStorageBackend(name, _ => CreateBackend(name, snapshot));
        return services;
    }

    /// <summary>
    /// 根据验证后的选项创建 Aliyun OSS 文件存储后端。
    /// </summary>
    /// <param name="name">文件存储后端名称。</param>
    /// <param name="options">已验证的 Aliyun OSS 存储选项。</param>
    /// <returns>拥有所创建 OSS 客户端的文件存储后端。</returns>
    internal static IFileStorageBackend CreateBackend(string name, BingAliyunOssStorageOptions options)
    {
        var config = new Configuration
        {
            Region = options.Region,
            Endpoint = options.Endpoint,
            UsePathStyle = options.UsePathStyle,
            SignatureVersion = "v4",
            DisableUploadCrc64Check = false
        };
        if (options.AccessKeyId != null)
            config.CredentialsProvider = string.IsNullOrWhiteSpace(options.SecurityToken)
                ? new StaticCredentialsProvider(options.AccessKeyId, options.AccessKeySecret!)
                : new StaticCredentialsProvider(options.AccessKeyId, options.AccessKeySecret!, options.SecurityToken);

        var client = new Client(config);
        return new AliyunOssFileStorageBackend(name, options, client, ownsClient: true);
    }
}
