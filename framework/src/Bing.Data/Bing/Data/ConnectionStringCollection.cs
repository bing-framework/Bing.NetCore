using Bing.Collections;

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
        get => this.GetOrDefault(DefaultConnectionStringName);
        set => this[DefaultConnectionStringName] = value;
    }

    /// <summary>
    /// 获取连接字符串
    /// </summary>
    /// <param name="name">连接字符串名称</param>
    public string GetConnectionString(string name) => ContainsKey(name) ? this.GetValueOrDefault(name) : Default;
}
