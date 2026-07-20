namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 表标识符
/// </summary>
public readonly record struct TableIdentifier
{
    /// <summary>
    /// 初始化一个<see cref="TableIdentifier"/>类型的实例
    /// </summary>
    /// <param name="schema">架构</param>
    /// <param name="name">表名</param>
    public TableIdentifier(string schema, string name)
    {
        Schema = schema?.Trim() ?? string.Empty;
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("表名不能为空。", nameof(name))
            : name.Trim();
    }

    /// <summary>
    /// 架构
    /// </summary>
    public string Schema { get; }

    /// <summary>
    /// 表名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 是否包含架构
    /// </summary>
    public bool HasSchema => string.IsNullOrWhiteSpace(Schema) == false;
}