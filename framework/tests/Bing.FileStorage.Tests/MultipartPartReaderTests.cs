using Bing.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FileStorage.Tests;

/// <summary>
/// 覆盖从可枚举集合读取数量且不调用枚举器等测试场景。
/// </summary>
public sealed class MultipartPartReaderTests
{
    /// <summary>
    /// 定义分片读取器测试使用的分片大小。
    /// </summary>
    private const int PartSize = 5 * 1024 * 1024;

    /// <summary>
    /// 验证旧 Provider 回退到一次完整列表并定位目标分片。
    /// </summary>
    [Fact]
    public async Task LegacyProvider_FallsBackToOneFullListAndFindsRequestedPart()
    {
        var provider = new LegacyMultipartStorage(new[] { Part(1), Part(2) });

        var result = await ((IMultipartFileStorage)provider).GetPartAsync(Upload(), 2);

        Assert.Equal(2, result!.Number);
        Assert.Equal(1, provider.ListCalls);
    }

    /// <summary>
    /// 验证可选分片读取器优先于完整列表。
    /// </summary>
    [Fact]
    public async Task OptionalReader_IsPreferredOverFullList()
    {
        var provider = new FastMultipartStorage(new[] { Part(1), Part(2) });

        var result = await ((IMultipartFileStorage)provider).GetPartAsync(Upload(), 2);

        Assert.Equal(2, result!.Number);
        Assert.Equal(0, provider.ListCalls);
        Assert.Equal(new[] { 2 }, provider.ReadRequests);
    }

    /// <summary>
    /// 验证旧 Provider 经过校验工厂包装后仍可回退点查。
    /// </summary>
    [Fact]
    public async Task LegacyProvider_ThroughValidatedFactoryStillUsesFallback()
    {
        var inner = new LegacyFileStorage(new[] { Part(1), Part(2) });
        using var services = CreateProvider(inner);
        var storage = services.GetRequiredService<IFileStorageFactory>().GetStorage("test", StorageScope.Host);

        var result = await ((IMultipartFileStorage)storage).GetPartAsync(Upload(), 2);

        Assert.Equal(2, result!.Number);
        Assert.Equal(1, inner.ListCalls);
    }

    /// <summary>
    /// 验证工厂在分片点查前拒绝未配置容器。
    /// </summary>
    [Fact]
    public async Task ValidatedFactory_RejectsUnconfiguredContainerBeforePointRead()
    {
        var inner = new FastFileStorage(new[] { Part(1), Part(2) });
        using var services = CreateProvider(inner);
        var storage = services.GetRequiredService<IFileStorageFactory>().GetStorage("test", StorageScope.Host);
        var upload = Upload(new FileAddress("unconfigured", "object/key"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ((IMultipartFileStorage)storage).GetPartAsync(upload, 1));

        Assert.Empty(inner.ReadRequests);
        Assert.Equal(0, inner.ListCalls);
    }

    /// <summary>
    /// 验证扩展点查在调用 Provider 前校验范围并处理取消。
    /// </summary>
    [Fact]
    public async Task Extension_RejectsOutOfRangePartAndHonorsCancellationBeforeProviderCall()
    {
        var provider = new FastMultipartStorage(new[] { Part(1) });
        var upload = Upload(length: 1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ((IMultipartFileStorage)provider).GetPartAsync(upload, 2));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ((IMultipartFileStorage)provider).GetPartAsync(upload, 1, cancellation.Token));

        Assert.Empty(provider.ReadRequests);
        Assert.Equal(0, provider.ListCalls);
    }

    /// <summary>
    /// 验证旧列表回退拒绝重复分片号。
    /// </summary>
    [Fact]
    public async Task LegacyFallback_RejectsDuplicatePartNumbers()
    {
        var provider = new LegacyMultipartStorage(new[] { Part(1), Part(1) });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ((IMultipartFileStorage)provider).GetPartAsync(Upload(), 1));

        Assert.Equal(1, provider.ListCalls);
    }

    /// <summary>
    /// 创建注册指定文件存储的测试容器。
    /// </summary>
    /// <param name="storage">测试所使用的存储实例。</param>
    /// <returns>注册了当前测试依赖的服务容器。</returns>
    private static ServiceProvider CreateProvider(IFileStorage storage)
    {
        var services = new ServiceCollection();
        services.AddBingFileStorage(options =>
        {
            options.DefaultStore = "test";
            options.Containers.Clear();
            options.Containers.Add("attachments");
        });
        services.AddBingFileStorageBackend("test", _ => new TestBackend(storage));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建指定地址及长度的分片上传测试会话。
    /// </summary>
    /// <param name="address">目标文件地址；未指定时使用测试地址。</param>
    /// <param name="length">内容长度，单位为字节。</param>
    /// <returns>包含写入请求及分片设置的测试上传会话。</returns>
    private static MultipartUpload Upload(FileAddress? address = null, long length = PartSize + 1) =>
        new("upload", new FileWriteRequest(address ?? new FileAddress("attachments", "tests/object"), length,
            FileWriteMode.CreateOnly), PartSize);

    /// <summary>
    /// 创建指定编号的文件分片。
    /// </summary>
    /// <param name="number">文件分片编号。</param>
    /// <returns>带指定编号及固定长度的测试分片。</returns>
    private static FilePart Part(int number) => new() { Number = number, Length = 1, ETag = "etag-" + number };

    /// <summary>
    /// 模拟仅实现旧式分片上传契约的文件存储。
    /// </summary>
    private class LegacyMultipartStorage : IMultipartFileStorage
    {
        /// <summary>
        /// 保存旧版分片存储返回的分片列表。
        /// </summary>
        private readonly IReadOnlyList<FilePart> _parts;
        /// <summary>
        /// 初始化一个 <see cref="LegacyMultipartStorage" /> 类型的实例。
        /// </summary>
        /// <param name="parts">已上传分片的信息集合。</param>
        public LegacyMultipartStorage(IReadOnlyList<FilePart> parts) => _parts = parts;
        /// <summary>
        /// 获取查询分片列表的累计调用次数。
        /// </summary>
        public int ListCalls { get; private set; }
        /// <inheritdoc />
        public Task<IReadOnlyList<FilePart>> ListPartsAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult(_parts);
        }
        /// <inheritdoc />
        public Task<MultipartUpload> CreateMultipartAsync(FileWriteRequest request, int partSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <inheritdoc />
        public Task<FilePart> UploadPartAsync(MultipartUpload upload, int partNumber, Stream source, string? sha256 = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <inheritdoc />
        public Task<FileWriteResult> CompleteMultipartAsync(MultipartUpload upload, IReadOnlyList<FilePart> parts, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <inheritdoc />
        public Task AbortMultipartAsync(MultipartUpload upload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// 为旧式文件存储提供分片读取能力的测试实现。
    /// </summary>
    private class FastMultipartStorage : LegacyMultipartStorage, IMultipartFilePartReader
    {
        /// <summary>
        /// 保存快速分片存储返回的分片列表。
        /// </summary>
        private readonly IReadOnlyList<FilePart> _parts;
        /// <summary>
        /// 初始化一个 <see cref="FastMultipartStorage" /> 类型的实例。
        /// </summary>
        /// <param name="parts">已上传分片的信息集合。</param>
        public FastMultipartStorage(IReadOnlyList<FilePart> parts) : base(parts) => _parts = parts;
        /// <summary>
        /// 获取已记录的读取请求。
        /// </summary>
        public List<int> ReadRequests { get; } = new();
        /// <inheritdoc />
        public Task<FilePart?> GetPartAsync(MultipartUpload upload, int partNumber, CancellationToken cancellationToken = default)
        {
            ReadRequests.Add(partNumber);
            return Task.FromResult(_parts.SingleOrDefault(part => part.Number == partNumber));
        }
    }

    /// <summary>
    /// 组合旧式对象存储和分片上传接口的测试实现。
    /// </summary>
    private class LegacyFileStorage : LegacyMultipartStorage, IFileStorage
    {
        /// <summary>
        /// 初始化一个 <see cref="LegacyFileStorage" /> 类型的实例。
        /// </summary>
        /// <param name="parts">已上传分片的信息集合。</param>
        public LegacyFileStorage(IReadOnlyList<FilePart> parts) : base(parts) { }
        /// <inheritdoc />
        public string Name => "test";
        /// <inheritdoc />
        public StorageScope Scope => StorageScope.Host;
        /// <inheritdoc />
        public FileStorageCapabilities Capabilities => FileStorageCapabilities.Multipart;
        /// <inheritdoc />
        public Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <inheritdoc />
        public Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <inheritdoc />
        public Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <inheritdoc />
        public Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// 为旧式存储提供文件与分片读取能力的测试实现。
    /// </summary>
    private sealed class FastFileStorage : FastMultipartStorage, IFileStorage
    {
        /// <summary>
        /// 初始化一个 <see cref="FastFileStorage" /> 类型的实例。
        /// </summary>
        /// <param name="parts">已上传分片的信息集合。</param>
        public FastFileStorage(IReadOnlyList<FilePart> parts) : base(parts) { }
        /// <inheritdoc />
        public string Name => "test";
        /// <inheritdoc />
        public StorageScope Scope => StorageScope.Host;
        /// <inheritdoc />
        public FileStorageCapabilities Capabilities => FileStorageCapabilities.Multipart;
        /// <inheritdoc />
        public Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <inheritdoc />
        public Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <inheritdoc />
        public Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        /// <inheritdoc />
        public Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// 组织TestBackend相关测试场景。
    /// </summary>
    private sealed class TestBackend : IFileStorageBackend
    {
        /// <summary>
        /// 保存测试后端包装的文件存储。
        /// </summary>
        private readonly IFileStorage _storage;
        /// <summary>
        /// 初始化一个 <see cref="TestBackend" /> 类型的实例。
        /// </summary>
        /// <param name="storage">实例使用的存储实现。</param>
        public TestBackend(IFileStorage storage) => _storage = storage;
        /// <inheritdoc />
        public string Name => "test";
        /// <inheritdoc />
        public IFileStorage Bind(StorageScope scope) => _storage;
    }
}
