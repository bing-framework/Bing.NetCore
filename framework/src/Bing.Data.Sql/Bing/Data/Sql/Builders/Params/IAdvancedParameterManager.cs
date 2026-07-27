namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 支持参数元数据的增强参数管理器。
/// </summary>
public interface IAdvancedParameterManager : IParameterManager
{
    /// <summary>
    /// 添加包含数据库类型和方向等元数据的参数。
    /// </summary>
    /// <param name="parameter">待添加的 SQL 参数；为 null 时实现可忽略该调用。</param>
    void Add(SqlParam parameter);

    /// <summary>
    /// 获取当前参数的元数据视图。
    /// </summary>
    /// <returns>以标准参数名称为键的 SQL 参数元数据集合。</returns>
    IReadOnlyDictionary<string, SqlParam> GetSqlParams();

    /// <summary>
    /// 导出用于数据库执行的参数值集合。
    /// </summary>
    /// <returns>以标准参数名称为键的参数值集合。</returns>
    IReadOnlyDictionary<string, object> ExportValues();
}
