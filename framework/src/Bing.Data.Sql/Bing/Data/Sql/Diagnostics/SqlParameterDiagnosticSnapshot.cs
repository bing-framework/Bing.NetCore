namespace Bing.Data.Sql.Diagnostics;

/// <summary>
/// SQL 参数诊断快照。
/// </summary>
public sealed class SqlParameterDiagnosticSnapshot
{
    /// <summary>
    /// 获取或设置传入参数对象的完整类型名称。
    /// </summary>
    /// <remarks>
    /// 参数源为空时为 null。
    /// </remarks>
    public string OriginalParameterType { get; set; }

    /// <summary>
    /// 获取或设置指示参数是否经过映射元数据补全或重绑定。
    /// </summary>
    public bool IsMetadataBound { get; set; }

    /// <summary>
    /// 获取或设置当前操作的参数诊断项。
    /// </summary>
    /// <remarks>
    /// 默认返回空集合。
    /// </remarks>
    public IReadOnlyList<SqlParameterDiagnosticInfo> Items { get; set; } = Array.Empty<SqlParameterDiagnosticInfo>();
}