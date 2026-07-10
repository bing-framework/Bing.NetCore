namespace Bing.Data.Sql;

/// <summary>
/// SQL 资源所有权
/// </summary>
public enum SqlResourceOwnership
{
    /// <summary>
    /// 内部拥有
    /// </summary>
    Owned,

    /// <summary>
    /// 外部提供
    /// </summary>
    External
}