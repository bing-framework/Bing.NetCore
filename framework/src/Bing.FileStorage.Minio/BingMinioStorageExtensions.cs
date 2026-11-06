using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage;

/// <summary>
/// MinIO 兼容服务的配置。
/// </summary>
/// <remarks>底层使用同一套 S3 Provider。</remarks>
public sealed class BingMinioStorageOptions
{
    /// <summary>
    /// 获取或设置MinIO API 地址。
    /// </summary>
    /// <remarks>默认连接本机 9000 端口。</remarks>
    public string Endpoint { get; set; } = "http://127.0.0.1:9000";

    /// <summary>
    /// 获取或设置MinIO 访问密钥。
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    /// 获取或设置MinIO 私密密钥。
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// 获取或设置用于 SigV4 的区域。
    /// </summary>
    /// <remarks>默认值为 <c>&quot;us-east-1&quot;</c>。</remarks>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// 获取或设置用于区分此应用对象键的前缀。
    /// </summary>
    /// <remarks>默认值为 <c>&quot;bing&quot;</c>。</remarks>
    public string StorageNamespace { get; set; } = "bing";

    /// <summary>
    /// 获取或设置MinIO 默认使用路径式寻址。
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// 获取或设置每次查询服务端分片清单的最大数量，范围为 1 至 1000。
    /// </summary>
    /// <remarks>默认值为 1000。</remarks>
    public int ListPartsPageSize { get; set; } = 1000;
}

/// <summary>
/// MinIO 文件存储服务注册扩展。
/// </summary>
/// <remarks>
/// MinIO Provider 注册入口。
/// </remarks>
public static class BingMinioStorageExtensions
{
    /// <summary>
    /// 将 MinIO 选项映射到 S3 存储并注册后端。
    /// </summary>
    /// <param name="services">要添加后端的依赖注入服务集合。</param>
    /// <param name="name">后端名称。</param>
    /// <param name="configure">配置 MinIO 存储选项的委托。</param>
    /// <returns>原依赖注入服务集合。</returns>
    /// <remarks>
    /// AccessKey 和 SecretKey 必须同时配置；注册时将 MinIO 选项映射到 S3 后端。
    /// </remarks>
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
