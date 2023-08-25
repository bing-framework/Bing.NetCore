namespace Bing.FreeSQL;

/// <summary>
/// FreeSQL包装器
/// </summary>
public class FreeSqlWrapper
{
    /// <summary>
    /// ORM
    /// </summary>
    public IFreeSql Orm { get; set; }
}

/// <summary>
/// FreeSQL包装器
/// </summary>
/// <typeparam name="TMark">标记类型</typeparam>
public class FreeSqlWrapper<TMark>
{
    /// <summary>
    /// ORM
    /// </summary>
    public IFreeSql<TMark> Orm { get; set; }
}
