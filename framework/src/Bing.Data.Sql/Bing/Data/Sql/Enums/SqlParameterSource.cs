namespace Bing.Data.Sql;

/// <summary>
/// 标识 SQL 参数的创建来源，供诊断和参数元数据追踪使用。
/// </summary>
public enum SqlParameterSource
{
    /// <summary>
    /// 未记录或无法确定参数来源。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 参数由基础参数管理器或通用参数扩展创建。
    /// </summary>
    Basic = 1,

    /// <summary>
    /// 参数由 Lambda 表达式解析产生。
    /// </summary>
    Lambda = 2,

    /// <summary>
    /// 参数由原生 SQL 文本操作产生。
    /// </summary>
    RawSql = 3,

    /// <summary>
    /// 参数由结构化 SQL 构建链产生。
    /// </summary>
    SqlBuilder = 4,

    /// <summary>
    /// 参数由调用方显式手工添加。
    /// </summary>
    Manual = 5,

    /// <summary>
    /// 参数由存储过程调用产生。
    /// </summary>
    Procedure = 6,

    /// <summary>
    /// 参数由系统级查询过滤器产生。
    /// </summary>
    SystemFilter = 7
}
