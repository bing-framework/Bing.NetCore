namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 列标识符
/// </summary>
public readonly record struct ColumnIdentifier
{
    /// <summary>
    /// 初始化一个<see cref="ColumnIdentifier"/>类型的实例
    /// </summary>
    /// <param name="name">列名</param>
    public ColumnIdentifier(string name)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("列名不能为空。", nameof(name))
            : name.Trim();
    }

    /// <summary>
    /// 列名
    /// </summary>
    public string Name { get; }
}