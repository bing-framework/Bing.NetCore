namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// Sql生成器操作项
/// </summary>
public class BuilderItem
{
    /// <summary>
    /// 获取操作名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取Sql生成器
    /// </summary>
    public ISqlBuilder Builder { get; }

    /// <summary>
    /// 获取冻结的 SQL 片段。
    /// </summary>
    /// <remarks>Builder 与 Fragment 二选一，片段项适合跨查询复用。</remarks>
    public SqlQueryFragment Fragment { get; }

    /// <summary>
    /// 获取此项是否指向创建它的 Builder 快照所拥有的子查询克隆。
    /// </summary>
    internal bool IsOwnedNestedClone { get; }

    /// <summary>
    /// 初始化一个 <see cref="BuilderItem"/> 类型的实例。
    /// </summary>
    /// <param name="name">操作名称</param>
    /// <param name="builder">Sql生成器</param>
    public BuilderItem(string name, ISqlBuilder builder)
    {
        Name = name;
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    /// <summary>
    /// 初始化一个冻结 SQL 片段操作项。
    /// </summary>
    /// <param name="name">操作名称。</param>
    /// <param name="fragment">冻结 SQL 片段。</param>
    public BuilderItem(string name, SqlQueryFragment fragment)
    {
        Name = name;
        Fragment = fragment ?? throw new ArgumentNullException(nameof(fragment));
    }

    /// <summary>
    /// 初始化一个由父 Builder 克隆过程创建的子查询项。
    /// </summary>
    /// <param name="name">操作名称。</param>
    /// <param name="builder">父 Builder 拥有的子查询克隆。</param>
    /// <param name="isOwnedNestedClone">是否为父 Builder 克隆过程创建。</param>
    internal BuilderItem(string name, ISqlBuilder builder, bool isOwnedNestedClone)
        : this(name, builder) => IsOwnedNestedClone = isOwnedNestedClone;
}
