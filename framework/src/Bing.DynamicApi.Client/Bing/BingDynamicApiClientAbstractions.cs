using Bing.DynamicApi.Contracts;
using System.Net;
using System.Text.Json;

namespace Bing.DynamicApi.Client;

/// <summary>
/// 提供随当前执行上下文变化的动态 API 请求信息。
/// </summary>
public interface IBingDynamicApiRequestContext
{
    /// <summary>
    /// 获取当前租户标识。
    /// </summary>
    string TenantId { get; }

    /// <summary>
    /// 获取当前关联标识。
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// 获取当前语言。
    /// </summary>
    string Language { get; }
}

/// <summary>
/// 在动态 API 元数据或业务请求发送前执行宿主自定义处理。
/// </summary>
public interface IBingDynamicApiRequestHandler
{
    /// <summary>
    /// 异步处理即将发送的请求。
    /// </summary>
    /// <param name="remoteName">远程服务名称。</param>
    /// <param name="request">即将发送的 HTTP 请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>宿主可在此注入凭据或请求上下文。</remarks>
    ValueTask HandleAsync(string remoteName, HttpRequestMessage request, CancellationToken cancellationToken);
}

/// <summary>
/// 将动态 API 参数转换为路由或查询字符串值。
/// </summary>
public interface IBingDynamicApiParameterConverter
{
    /// <summary>
    /// 将参数值转换为 HTTP 字符串表示。
    /// </summary>
    /// <param name="value">参数值，可为 null。</param>
    /// <param name="valueType">参数声明类型。</param>
    /// <param name="parameter">参数绑定描述。</param>
    /// <returns>参数的字符串表示；返回 null 表示省略该值。</returns>
    string ConvertToString(object value, Type valueType, BingDynamicApiParameterDefinition parameter);
}

/// <summary>
/// 提供可替换的动态 API JSON 序列化行为。
/// </summary>
public interface IBingDynamicApiJsonSerializer
{
    /// <summary>
    /// 序列化 JSON 请求体。
    /// </summary>
    /// <param name="value">请求体值。</param>
    /// <param name="valueType">请求体声明类型。</param>
    /// <param name="options">JSON 序列化选项。</param>
    /// <returns>包含 JSON 请求体的 HTTP 内容。</returns>
    HttpContent Serialize(object value, Type valueType, JsonSerializerOptions options);

    /// <summary>
    /// 异步反序列化成功响应。
    /// </summary>
    /// <param name="content">响应内容。</param>
    /// <param name="returnType">目标返回类型。</param>
    /// <param name="options">JSON 序列化选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>反序列化结果；响应表示空值时可返回 null。</returns>
    Task<object> DeserializeAsync(HttpContent content, Type returnType, JsonSerializerOptions options, CancellationToken cancellationToken);
}

/// <summary>
/// 决定可重放动态 API 请求是否重试及其间隔。
/// </summary>
public interface IBingDynamicApiRetryPolicy
{
    /// <summary>
    /// 判断失败的可重放请求是否应重试。
    /// </summary>
    /// <param name="remoteName">远程服务名称。</param>
    /// <param name="attempt">已完成的请求尝试次数，从零开始。</param>
    /// <param name="statusCode">HTTP 响应状态；传输异常时为空。</param>
    /// <param name="exception">传输异常；收到 HTTP 响应时为空。</param>
    /// <param name="remoteOptions">远程服务配置。</param>
    /// <returns>应重试时返回 true，否则返回 false。</returns>
    bool ShouldRetry(
        string remoteName,
        int attempt,
        HttpStatusCode? statusCode,
        HttpRequestException exception,
        BingDynamicApiRemoteOptions remoteOptions);

    /// <summary>
    /// 获取下一次请求前的等待时长。
    /// </summary>
    /// <param name="remoteName">远程服务名称。</param>
    /// <param name="attempt">已完成的请求尝试次数，从零开始。</param>
    /// <param name="remoteOptions">远程服务配置。</param>
    /// <returns>下一次尝试前的等待时长。</returns>
    TimeSpan GetDelay(string remoteName, int attempt, BingDynamicApiRemoteOptions remoteOptions);
}

/// <summary>
/// 提供未配置宿主访问器时的空请求上下文。
/// </summary>
internal sealed class EmptyBingDynamicApiRequestContext : IBingDynamicApiRequestContext
{
    /// <inheritdoc />
    public string TenantId => null;
    /// <inheritdoc />
    public string CorrelationId => null;
    /// <inheritdoc />
    public string Language => null;
}

/// <summary>
/// 提供简单参数的默认字符串转换。
/// </summary>
internal sealed class DefaultBingDynamicApiParameterConverter : IBingDynamicApiParameterConverter
{
    /// <inheritdoc />
    public string ConvertToString(object value, Type valueType, BingDynamicApiParameterDefinition parameter)
    {
        if (value == null)
            return null;

        var type = Nullable.GetUnderlyingType(valueType) ?? valueType;
        if (type == typeof(object))
        {
            return value is IFormattable objectFormattable
                ? objectFormattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture)
                : value.ToString();
        }
        if (!IsSimple(type))
            throw new NotSupportedException($"参数 '{parameter.Name}' 的类型 '{valueType.FullName}' 不能作为路由或查询参数。请注册自定义 {nameof(IBingDynamicApiParameterConverter)}。");

        if (type.IsEnum)
            return Enum.GetName(type, value) ?? value.ToString();
        if (value is IFormattable formattable)
            return formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
        return value.ToString();
    }

    /// <summary>
    /// 判断类型是否支持默认参数转换。
    /// </summary>
    /// <param name="type">参数类型。</param>
    /// <returns>为受支持的简单类型时返回 true，否则返回 false。</returns>
    internal static bool IsSimple(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
               type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) || type == typeof(Uri);
    }
}

/// <summary>
/// 使用 System.Text.Json 序列化动态 API 内容。
/// </summary>
internal sealed class SystemTextJsonBingDynamicApiSerializer : IBingDynamicApiJsonSerializer
{
    /// <inheritdoc />
    public HttpContent Serialize(object value, Type valueType, JsonSerializerOptions options)
    {
        var json = JsonSerializer.Serialize(value, valueType, options);
        return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    /// <inheritdoc />
    public async Task<object> DeserializeAsync(HttpContent content, Type returnType, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync(stream, returnType, options, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// 为可重放请求提供默认失败重试判断。
/// </summary>
internal sealed class DefaultBingDynamicApiRetryPolicy : IBingDynamicApiRetryPolicy
{
    /// <inheritdoc />
    public bool ShouldRetry(
        string remoteName,
        int attempt,
        HttpStatusCode? statusCode,
        HttpRequestException exception,
        BingDynamicApiRemoteOptions remoteOptions)
    {
        if (exception != null)
            return true;
        return statusCode == HttpStatusCode.RequestTimeout
               || statusCode == (HttpStatusCode)429
               || (statusCode.HasValue && (int)statusCode.Value >= 500);
    }

    /// <inheritdoc />
    public TimeSpan GetDelay(string remoteName, int attempt, BingDynamicApiRemoteOptions remoteOptions) => remoteOptions.RetryDelay;
}
