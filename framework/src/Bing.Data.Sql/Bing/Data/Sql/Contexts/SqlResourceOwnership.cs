namespace Bing.Data.Sql;

/// <summary>
/// 指示 SQL 连接或事务等资源由当前执行链创建并负责释放，还是由外部调用方管理。
/// </summary>
public enum SqlResourceOwnership
{
    /// <summary>
    /// 资源由当前执行链拥有，并在适当的生命周期结束时负责释放。
    /// </summary>
    Owned,

    /// <summary>
    /// 资源由外部调用方提供，当前执行链不得擅自释放其生命周期。
    /// </summary>
    External
}