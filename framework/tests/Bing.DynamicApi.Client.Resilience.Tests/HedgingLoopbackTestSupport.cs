using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Bing.DynamicApi.Client.Resilience.Tests;

internal sealed record HedgingLoopbackRequest(
    string Method,
    string Target,
    IReadOnlyDictionary<string, string> Headers,
    string Body)
{
    public string Path => Target.Split('?', 2)[0];
}

internal sealed record HedgingLoopbackResponse(HttpStatusCode StatusCode, string Body, string ContentType = "application/json", bool Stall = false)
{
    public static HedgingLoopbackResponse Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status, body);
    public static HedgingLoopbackResponse Hang() => new(HttpStatusCode.OK, string.Empty, Stall: true);
}

internal sealed class HedgingLoopbackServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<HedgingLoopbackRequest, CancellationToken, Task<HedgingLoopbackResponse>> _respond;
    private readonly System.Collections.Concurrent.ConcurrentBag<Task> _requests = new();
    private readonly Task _accept;

    public HedgingLoopbackServer(Func<HedgingLoopbackRequest, CancellationToken, Task<HedgingLoopbackResponse>> respond)
    {
        _respond = respond;
        _listener.Start();
        Origin = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
        _accept = AcceptAsync();
    }

    public Uri Origin { get; }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                _requests.Add(ServeAsync(client));
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (_stop.IsCancellationRequested) { }
        catch (SocketException) when (_stop.IsCancellationRequested) { }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true);
                var firstLine = await reader.ReadLineAsync().WaitAsync(_stop.Token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(firstLine)) return;
                var requestLine = firstLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                if (requestLine.Length < 2) return;

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().WaitAsync(_stop.Token).ConfigureAwait(false)))
                {
                    var colon = line.IndexOf(':');
                    if (colon > 0)
                        headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
                }

                var body = await ReadBodyAsync(reader, headers, _stop.Token).ConfigureAwait(false);
                var response = await _respond(new(requestLine[0], requestLine[1], headers, body), _stop.Token).ConfigureAwait(false);
                if (response.Stall)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, _stop.Token).ConfigureAwait(false);
                    return;
                }

                await WriteResponseAsync(stream, response, _stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }
    }

    private static async Task<string> ReadBodyAsync(StreamReader reader, IReadOnlyDictionary<string, string> headers, CancellationToken token)
    {
        if (!headers.TryGetValue("Content-Length", out var contentLengthValue) ||
            !int.TryParse(contentLengthValue, out var contentLength) || contentLength <= 0)
            return string.Empty;

        var body = new char[contentLength];
        var offset = 0;
        while (offset < body.Length)
        {
            var read = await reader.ReadAsync(body, offset, body.Length - offset).WaitAsync(token).ConfigureAwait(false);
            if (read == 0) break;
            offset += read;
        }
        return new string(body, 0, offset);
    }

    private static async Task WriteResponseAsync(NetworkStream stream, HedgingLoopbackResponse response, CancellationToken token)
    {
        var body = Encoding.UTF8.GetBytes(response.Body);
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {(int)response.StatusCode} {response.StatusCode}\r\n" +
            $"Content-Type: {response.ContentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers, token).ConfigureAwait(false);
        await stream.WriteAsync(body, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        await _accept.ConfigureAwait(false);
        await Task.WhenAll(_requests.ToArray()).ConfigureAwait(false);
        _stop.Dispose();
    }
}
