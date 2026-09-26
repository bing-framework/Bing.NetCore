using Bing.Http;
using Bing.Http.Clients;
using System.Collections;
using System.Net.Http;
using System.Text.Json;

namespace Bing.DynamicApi.Client;

/// <summary>
/// 将远程 HTTP 失败响应转换为客户端异常。
/// </summary>
internal static class BingDynamicApiHttpErrors
{
    /// <summary>
    /// 在请求选项中保存取消令牌，供后续错误体读取沿用。
    /// </summary>
    private static readonly HttpRequestOptionsKey<CancellationToken> CancellationTokenKey =
        new("Bing.DynamicApi.Client.CancellationToken");

    /// <summary>
    /// 保存请求的取消令牌。
    /// </summary>
    /// <param name="request">待发送的请求。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    public static void SetRequestCancellationToken(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Options.Set(CancellationTokenKey, cancellationToken);
    }

    /// <summary>
    /// 异步构建远程调用异常。
    /// </summary>
    /// <param name="response">失败的 HTTP 响应。</param>
    /// <param name="jsonOptions">错误信息反序列化选项。</param>
    /// <param name="cancellationToken">取消令牌；不可取消时沿用请求中的令牌。</param>
    /// <returns>包含 HTTP 状态和远程错误信息的异常。</returns>
    public static async Task<BingRemoteCallException> CreateExceptionAsync(
        HttpResponseMessage response,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken = default)
    {
        if (!cancellationToken.CanBeCanceled && response.RequestMessage?.Options.TryGetValue(CancellationTokenKey, out var requestCancellationToken) == true)
            cancellationToken = requestCancellationToken;

        var body = response.Content == null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var error = TryReadError(body, jsonOptions) ?? new RemoteServiceErrorInfo(
            string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "远程请求失败。" : body);
        var exception = new BingRemoteCallException(error)
        {
            HttpStatusCode = (int)response.StatusCode
        };
        return exception;
    }

    /// <summary>
    /// 尝试解析响应中的远程错误信息。
    /// </summary>
    /// <param name="body">响应文本。</param>
    /// <param name="jsonOptions">反序列化选项。</param>
    /// <returns>解析得到的错误信息；内容为空或无法解析时返回 null。</returns>
    private static RemoteServiceErrorInfo TryReadError(string body, JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var nested))
                root = nested;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var message = ReadString(root, "message") ?? ReadString(root, "Message") ?? body;
            var info = new RemoteServiceErrorInfo(
                message,
                ReadString(root, "details") ?? ReadString(root, "Details"),
                ReadString(root, "code") ?? ReadString(root, "Code"));

            if (TryGetProperty(root, "data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                var values = new Hashtable(StringComparer.OrdinalIgnoreCase);
                foreach (var property in data.EnumerateObject())
                    values[property.Name] = ToObject(property.Value);
                info.Data = values;
            }

            return info;
        }
        catch (JsonException)
        {
            try
            {
                return JsonSerializer.Deserialize<RemoteServiceErrorInfo>(body, jsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 查找不区分大小写的 JSON 属性。
    /// </summary>
    /// <param name="element">JSON 对象。</param>
    /// <param name="name">属性名称。</param>
    /// <param name="value">匹配的属性值。</param>
    /// <returns>找到属性时返回 true，否则返回 false。</returns>
    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
            return true;
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    /// <summary>
    /// 读取 JSON 字符串属性。
    /// </summary>
    /// <param name="element">JSON 对象。</param>
    /// <param name="name">属性名称。</param>
    /// <returns>字符串属性值；属性不存在或不是字符串时返回 null。</returns>
    private static string ReadString(JsonElement element, string name)
    {
        return TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>
    /// 转换错误附加数据中的 JSON 值。
    /// </summary>
    /// <param name="value">待转换的 JSON 值。</param>
    /// <returns>简单 CLR 值；JSON 空值返回 null，复杂值返回原始 JSON 文本。</returns>
    private static object ToObject(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetInt64(out var integer) ? integer : value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }
}
