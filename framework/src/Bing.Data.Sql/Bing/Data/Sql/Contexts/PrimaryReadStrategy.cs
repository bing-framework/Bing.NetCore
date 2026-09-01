namespace Bing.Data.Sql;

/// <summary>
/// 指定查询请求主库读取时采用的处理策略。
/// </summary>
public enum PrimaryReadStrategy
{
    /// <summary>
    /// 不做特殊处理，由数据源自行决定读取位置。
    /// </summary>
    None,

    /// <summary>
    /// 切换到主库数据源读取。
    /// </summary>
    PrimaryDataSource,

    /// <summary>
    /// 使用事务保障主库读取。
    /// </summary>
    Transaction
}