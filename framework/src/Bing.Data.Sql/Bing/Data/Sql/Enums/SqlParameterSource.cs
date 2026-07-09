namespace Bing.Data.Sql;

/// <summary>
/// Sql参数来源
/// </summary>
public enum SqlParameterSource
{
    /// <summary>
    /// 未知来源
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 旧参数链路
    /// </summary>
    Legacy = 1,

    /// <summary>
    /// Lambda 表达式
    /// </summary>
    Lambda = 2,

    /// <summary>
    /// 原生 Sql
    /// </summary>
    RawSql = 3,

    /// <summary>
    /// Sql 生成器
    /// </summary>
    SqlBuilder = 4,

    /// <summary>
    /// 手工添加
    /// </summary>
    Manual = 5,

    /// <summary>
    /// 存储过程
    /// </summary>
    Procedure = 6,

    /// <summary>
    /// 系统过滤器
    /// </summary>
    SystemFilter = 7
}
