namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 支持参数元数据的增强参数管理器。
/// </summary>
/// <remarks>
/// 实例包含可变参数状态，不支持并发读写。跨线程共享实例时调用方必须自行同步；并发操作应使用独立的克隆实例。
/// </remarks>
public interface IAdvancedParameterManager : IParameterManager
{
    /// <summary>
    /// 添加包含数据库类型和方向等元数据的参数。
    /// </summary>
    /// <param name="parameter">待添加的 SQL 参数；为 null 时实现可忽略该调用。</param>
    void Add(SqlParam parameter);

    /// <summary>
    /// 获取当前参数的元数据快照。
    /// </summary>
    /// <returns>调用时刻以标准参数名称为键的独立 SQL 参数元数据集合；返回的 <see cref="SqlParam"/> 容器可修改但不会反写管理器。</returns>
    IReadOnlyDictionary<string, SqlParam> GetSqlParams();

    /// <summary>
    /// 导出用于数据库执行的参数值集合。
    /// </summary>
    /// <returns>调用时刻以标准参数名称为键的独立参数值集合；后续写入不会改变返回集合。</returns>
    IReadOnlyDictionary<string, object> ExportValues();
}
