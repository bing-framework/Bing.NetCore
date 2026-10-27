using System.Net;

namespace Bing.Http.Resilience;

/// <summary>响应体持有管线代租约，流或响应释放后才允许回收旧代。</summary>
internal sealed class GenerationHttpContent : HttpContent
{
    private readonly HttpContent _inner;
    private readonly LeaseOwner _owner;
    private int _disposed;
    public GenerationHttpContent(HttpContent inner, IDisposable lease)
    {
        _inner = inner; _owner = new(lease);
        foreach (var header in inner.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
    }
    protected override bool TryComputeLength(out long length)
    {
        length = _inner.Headers.ContentLength.GetValueOrDefault(); return _inner.Headers.ContentLength.HasValue;
    }
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => _inner.CopyToAsync(stream, context);
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
        _inner.CopyToAsync(stream, context, cancellationToken);
    protected override async Task<Stream> CreateContentReadStreamAsync() => new LeaseStream(await _inner.ReadAsStreamAsync().ConfigureAwait(false), _owner);
    protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
        new LeaseStream(await _inner.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), _owner);
    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0) { try { _inner.Dispose(); } finally { _owner.Dispose(); } }
        base.Dispose(disposing);
    }
    private sealed class LeaseOwner(IDisposable lease) : IDisposable
    {
        private IDisposable? _lease = lease;
        public void Dispose() => Interlocked.Exchange(ref _lease, null)?.Dispose();
    }
    private sealed class LeaseStream(Stream inner, LeaseOwner owner) : Stream
    {
        private int _disposed;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0) { try { inner.Dispose(); } finally { owner.Dispose(); } }
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                try { await inner.DisposeAsync().ConfigureAwait(false); } finally { owner.Dispose(); }
            GC.SuppressFinalize(this);
        }
    }
}
