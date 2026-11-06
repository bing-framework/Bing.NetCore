using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Xunit;

namespace Bing.FileStorage.S3.Tests;

/// <summary>验证实机夹具的并行建桶、异步等待和异常传播，不访问外部服务。</summary>
public sealed class MinioBucketFixtureTests
{
    [Fact]
    public async Task ExistingBucket_DoesNotCreate()
    {
        using var client = new BucketClient();
        await MinioBucketFixture.EnsureBucketAsync(client);
        Assert.Equal(1, client.Lookups);
        Assert.Equal(0, client.Creates);
    }

    [Fact]
    public async Task MissingBucket_WaitsForCreationBeforeReturning()
    {
        var created = new TaskCompletionSource<PutBucketResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new BucketClient
        {
            Lookup = (_, _) => throw Error("NoSuchBucket"),
            Create = _ => created.Task
        };
        var initialize = MinioBucketFixture.EnsureBucketAsync(client);
        Assert.False(initialize.IsCompleted);
        Assert.Equal(1, client.Creates);
        created.SetResult(new PutBucketResponse());
        await initialize;
    }

    [Fact]
    public async Task ConcurrentMissingBucket_VerifiesBucketCreatedByOtherInitializer()
    {
        var bothLookedUp = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new BucketClient
        {
            Lookup = async (number, ct) =>
            {
                if (number > 2) return new GetBucketLocationResponse();
                if (number == 2) bothLookedUp.TrySetResult(true);
                await bothLookedUp.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                throw Error("NoSuchBucket");
            },
            Create = number => number == 1
                ? Task.FromResult(new PutBucketResponse()) : throw Error("BucketAlreadyOwnedByYou")
        };
        await Task.WhenAll(MinioBucketFixture.EnsureBucketAsync(client), MinioBucketFixture.EnsureBucketAsync(client));
        Assert.Equal(2, client.Creates);
        Assert.Equal(3, client.Lookups);
    }

    [Theory]
    [InlineData("AccessDenied")]
    [InlineData("ServiceUnavailable")]
    public async Task LookupFailure_IsNotTreatedAsMissingBucket(string code)
    {
        var error = Error(code);
        using var client = new BucketClient { Lookup = (_, _) => throw error };
        Assert.Same(error, await Assert.ThrowsAsync<AmazonS3Exception>(() => MinioBucketFixture.EnsureBucketAsync(client)));
        Assert.Equal(0, client.Creates);
    }

    [Theory]
    [InlineData("BucketAlreadyExists")]
    [InlineData("AccessDenied")]
    public async Task CreationFailure_IsNotTreatedAsConcurrentSuccess(string code)
    {
        var error = Error(code);
        using var client = new BucketClient
        {
            Lookup = (_, _) => throw Error("NoSuchBucket"), Create = _ => throw error
        };
        Assert.Same(error, await Assert.ThrowsAsync<AmazonS3Exception>(() => MinioBucketFixture.EnsureBucketAsync(client)));
        Assert.Equal(1, client.Lookups);
    }

    [Fact]
    public async Task ConcurrentCreation_StillRequiresReadableBucket()
    {
        var error = Error("AccessDenied");
        using var client = new BucketClient
        {
            Lookup = (number, _) => throw (number == 1 ? Error("NoSuchBucket") : error),
            Create = _ => throw Error("BucketAlreadyOwnedByYou")
        };
        Assert.Same(error, await Assert.ThrowsAsync<AmazonS3Exception>(() => MinioBucketFixture.EnsureBucketAsync(client)));
    }

    [Fact]
    public async Task Cancellation_IsPropagatedWithoutCreatingBucket()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = new BucketClient { Lookup = (_, ct) => Task.FromCanceled<GetBucketLocationResponse>(ct) };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MinioBucketFixture.EnsureBucketAsync(client, cancellation.Token));
        Assert.Equal(0, client.Creates);
    }

    private static AmazonS3Exception Error(string code) => new(code) { ErrorCode = code };

    /// <summary>在 SDK 边界控制请求顺序和异常，以确定性重现并行建桶。</summary>
    private sealed class BucketClient : AmazonS3Client
    {
        private int _lookups;
        private int _creates;
        public int Lookups => _lookups;
        public int Creates => _creates;
        public Func<int, CancellationToken, Task<GetBucketLocationResponse>> Lookup { get; init; }
            = (_, _) => Task.FromResult(new GetBucketLocationResponse());
        public Func<int, Task<PutBucketResponse>> Create { get; init; }
            = _ => Task.FromResult(new PutBucketResponse());
        public BucketClient() : base(new BasicAWSCredentials("test", "test"),
            new AmazonS3Config { ServiceURL = "http://127.0.0.1:1", ForcePathStyle = true }) { }
        public override Task<GetBucketLocationResponse> GetBucketLocationAsync(GetBucketLocationRequest request, CancellationToken ct = default)
            => Lookup(Interlocked.Increment(ref _lookups), ct);
        public override Task<PutBucketResponse> PutBucketAsync(PutBucketRequest request, CancellationToken ct = default)
            => Create(Interlocked.Increment(ref _creates));
    }
}
