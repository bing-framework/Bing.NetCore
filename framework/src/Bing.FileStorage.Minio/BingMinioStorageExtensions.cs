using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage;

/// <summary>MinIO 兼容服务的配置；底层使用同一套 S3 Provider。</summary>
public sealed class BingMinioStorageOptions
{
    /// <summary>MinIO API 地址，默认连接本机 9000 端口。</summary>
    public string Endpoint { get; set; } = "http://127.0.0.1:9000";

    /// <summary>MinIO 访问密钥。</summary>
    public string? AccessKey { get; set; }

    /// <summary>MinIO 私密密钥。</summary>
    public string? SecretKey { get; set; }

    /// <summary>用于 SigV4 的区域。</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>用于区分此应用对象键的前缀。</summary>
    public string StorageNamespace { get; set; } = "bing";

    /// <summary>MinIO 默认使用路径式寻址。</summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>每次查询服务端分片清单的最大数量，范围为 1 至 1000。</summary>
    public int ListPartsPageSize { get; set; } = 1000;
}

/// <summary>MinIO Provider 注册入口。</summary>
public static class BingMinioStorageExtensions
{
    /// <summary>注册一个 MinIO 命名存储实例，具体操作由 S3 Provider 执行。</summary>
    public static IServiceCollection AddBingMinioStorage(
        this IServiceCollection services,
        string name,
        Action<BingMinioStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var minio = new BingMinioStorageOptions();
        configure(minio);
        if (string.IsNullOrWhiteSpace(minio.AccessKey) || string.IsNullOrWhiteSpace(minio.SecretKey))
            throw new ArgumentException("MinIO AccessKey 与 SecretKey 必须显式配置。", nameof(configure));

        services.AddBingS3Storage(name, s3 =>
        {
            s3.ServiceUrl = minio.Endpoint;
            s3.Region = minio.Region;
            s3.AccessKey = minio.AccessKey;
            s3.SecretKey = minio.SecretKey;
            s3.StorageNamespace = minio.StorageNamespace;
            s3.ForcePathStyle = minio.ForcePathStyle;
            s3.ListPartsPageSize = minio.ListPartsPageSize;
        });
        return services;
    }
}
