namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 增强参数管理器
/// </summary>
public interface IAdvancedParameterManager : IParameterManager
{
    /// <summary>
    /// 添加增强参数
    /// </summary>
    /// <param name="parameter">Sql 参数</param>
    void Add(SqlParam parameter);

    /// <summary>
    /// 获取增强参数集合
    /// </summary>
    /// <returns>增强参数集合</returns>
    IReadOnlyDictionary<string, SqlParam> GetSqlParams();

    /// <summary>
    /// 导出参数值集合
    /// </summary>
    /// <returns>参数值集合</returns>
    IReadOnlyDictionary<string, object> ExportValues();
}
