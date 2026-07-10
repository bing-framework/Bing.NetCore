namespace Bing.Data.Sql;

/// <summary>
/// 主库读取策略
/// </summary>
public enum PrimaryReadStrategy
{
    /// <summary>
    /// 不做特殊处理
    /// </summary>
    None,

    /// <summary>
    /// 切换到主库数据源
    /// </summary>
    PrimaryDataSource,

    /// <summary>
    /// 使用事务保障主库读取
    /// </summary>
    Transaction
}