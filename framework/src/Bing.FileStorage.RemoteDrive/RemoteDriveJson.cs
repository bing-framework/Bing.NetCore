using System.Globalization;
using System.Text.Json;

namespace Bing.FileStorage.RemoteDrive;

/// <summary>
/// 兼容开放平台标识编码的 JSON 工具。
/// </summary>
/// <remarks>
/// 兼容开放平台数字标识的字符串/数值两种 JSON 编码。
/// </remarks>
public static class RemoteDriveJson
{
    /// <summary>
    /// 读取 JSON 对象中指定属性的文本值。
    /// </summary>
    /// <param name="value">包含属性的 JSON 元素。</param>
    /// <param name="name">属性名称。</param>
    /// <returns>属性存在且不是 null 或 undefined 时返回其文本表示；否则返回空字符串。</returns>
    public static string Text(this JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var item)
        && item.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) ? item.ToString() : "";
    /// <summary>
    /// 读取 JSON 对象中指定属性的整数值。
    /// </summary>
    /// <param name="value">包含属性的 JSON 元素。</param>
    /// <param name="name">属性名称。</param>
    /// <returns>属性可按不变区域性解析为整数时返回该值；否则返回 0。</returns>
    public static long Number(this JsonElement value, string name) => long.TryParse(value.Text(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;
    /// <summary>
    /// 读取 JSON 对象中指定的子元素。
    /// </summary>
    /// <param name="value">包含子元素的 JSON 元素。</param>
    /// <param name="name">子元素名称。</param>
    /// <returns>子元素存在时返回其值；否则返回默认 JSON 元素。</returns>
    public static JsonElement Child(this JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var child) ? child : default;
    /// <summary>
    /// 从授权响应中解析并验证远程存储凭据。
    /// </summary>
    /// <param name="value">包含授权字段的 JSON 响应。</param>
    /// <returns>包含访问令牌、刷新令牌和过期时间的凭据。</returns>
    /// <remarks>
    /// 访问令牌、刷新令牌为空或有效期不为正数时抛出访问拒绝错误。
    /// </remarks>
    public static RemoteDriveCredentials Credentials(JsonElement value)
    {
        var result = new RemoteDriveCredentials { AccessToken = value.Text("access_token"), RefreshToken = value.Text("refresh_token"),
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + checked(value.Number("expires_in") * 1000) };
        if (result.AccessToken.Length == 0 || result.RefreshToken.Length == 0 || value.Number("expires_in") <= 0)
            throw new FileStorageException(FileStorageError.AccessDenied, "授权返回的凭据无效。");
        return result;
    }
}
