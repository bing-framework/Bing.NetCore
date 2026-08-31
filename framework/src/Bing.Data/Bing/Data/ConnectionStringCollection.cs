namespace Bing.Data;

/// <summary>
/// 连接字符串集合
/// </summary>
[Serializable]
public class ConnectionStringCollection : Dictionary<string, string>
{
    /// <summary>
    /// 默认连接字符串名称，值：Default
    /// </summary>
    public const string DefaultConnectionStringName = "Default";

    /// <summary>
    /// 默认连接字符串
    /// </summary>
    public string Default
    {
        get => TryGetValue(DefaultConnectionStringName, out var value) ? value : null;
        set => this[DefaultConnectionStringName] = value;
    }

    /// <summary>
    /// 获取连接字符串
    /// </summary>
    /// <param name="name">连接字符串名称</param>
    /// <returns>指定名称的连接字符串；名称为空或未找到时返回默认连接字符串。</returns>
    public string GetConnectionString(string name) => string.IsNullOrWhiteSpace(name)
        ? Default
        : TryGetValue(name, out var value) ? value : Default;
}
