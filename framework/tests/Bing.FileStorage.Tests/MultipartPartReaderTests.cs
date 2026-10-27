using Bing.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FileStorage.Tests;

public sealed class MultipartPartReaderTests
{
    private const int PartSize = 5 * 1024 * 1024;

    [Fact]
    public async Task LegacyProvider_FallsBackToOneFullListAndFindsRequestedPart()
    {
        var provider = new LegacyMultipartStorage(new[] { Part(1), Part(2) });

        var result = await ((IMultipartFileStorage)provider).GetPartAsync(Upload(), 2);

        Assert.Equal(2, result!.Number);
        Assert.Equal(1, provider.ListCalls);
    }

    [Fact]
    public async Task OptionalReader_IsPreferredOverFullList()
    {
        var provider = new FastMultipartStorage(new[] { Part(1), Part(2) });

        var result = await ((IMultipartFileStorage)provider).GetPartAsync(Upload(), 2);

        Assert.Equal(2, result!.Number);
        Assert.Equal(0, provider.ListCalls);
        Assert.Equal(new[] { 2 }, provider.ReadRequests);
    }

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

    [Fact]
    public async Task LegacyFallback_RejectsDuplicatePartNumbers()
    {
        var provider = new LegacyMultipartStorage(new[] { Part(1), Part(1) });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ((IMultipartFileStorage)provider).GetPartAsync(Upload(), 1));

        Assert.Equal(1, provider.ListCalls);
    }

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

    private static MultipartUpload Upload(FileAddress? address = null, long length = PartSize + 1) =>
        new("upload", new FileWriteRequest(address ?? new FileAddress("attachments", "tests/object"), length,
            FileWriteMode.CreateOnly), PartSize);

    private static FilePart Part(int number) => new() { Number = number, Length = 1, ETag = "etag-" + number };

    private class LegacyMultipartStorage : IMultipartFileStorage
    {
        private readonly IReadOnlyList<FilePart> _parts;
        public LegacyMultipartStorage(IReadOnlyList<FilePart> parts) => _parts = parts;
        public int ListCalls { get; private set; }
        public Task<IReadOnlyList<FilePart>> ListPartsAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult(_parts);
        }
        public Task<MultipartUpload> CreateMultipartAsync(FileWriteRequest request, int partSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FilePart> UploadPartAsync(MultipartUpload upload, int partNumber, Stream source, string? sha256 = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileWriteResult> CompleteMultipartAsync(MultipartUpload upload, IReadOnlyList<FilePart> parts, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AbortMultipartAsync(MultipartUpload upload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private class FastMultipartStorage : LegacyMultipartStorage, IMultipartFilePartReader
    {
        private readonly IReadOnlyList<FilePart> _parts;
        public FastMultipartStorage(IReadOnlyList<FilePart> parts) : base(parts) => _parts = parts;
        public List<int> ReadRequests { get; } = new();
        public Task<FilePart?> GetPartAsync(MultipartUpload upload, int partNumber, CancellationToken cancellationToken = default)
        {
            ReadRequests.Add(partNumber);
            return Task.FromResult(_parts.SingleOrDefault(part => part.Number == partNumber));
        }
    }

    private class LegacyFileStorage : LegacyMultipartStorage, IFileStorage
    {
        public LegacyFileStorage(IReadOnlyList<FilePart> parts) : base(parts) { }
        public string Name => "test";
        public StorageScope Scope => StorageScope.Host;
        public FileStorageCapabilities Capabilities => FileStorageCapabilities.Multipart;
        public Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FastFileStorage : FastMultipartStorage, IFileStorage
    {
        public FastFileStorage(IReadOnlyList<FilePart> parts) : base(parts) { }
        public string Name => "test";
        public StorageScope Scope => StorageScope.Host;
        public FileStorageCapabilities Capabilities => FileStorageCapabilities.Multipart;
        public Task<FileWriteResult> SaveAsync(FileWriteRequest request, Stream source, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReadAsync(FileAddress address, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FileProperties?> GetPropertiesAsync(FileAddress address, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(FileAddress address, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestBackend : IFileStorageBackend
    {
        private readonly IFileStorage _storage;
        public TestBackend(IFileStorage storage) => _storage = storage;
        public string Name => "test";
        public IFileStorage Bind(StorageScope scope) => _storage;
    }
}
