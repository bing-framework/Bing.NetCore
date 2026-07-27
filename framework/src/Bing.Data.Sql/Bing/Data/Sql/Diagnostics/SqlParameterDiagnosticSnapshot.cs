namespace Bing.Data.Sql.Diagnostics;

/// <summary>SQL 参数诊断快照。</summary>
public sealed class SqlParameterDiagnosticSnapshot
{
    /// <summary>
    /// 传入参数对象的完整类型名称；参数源为空时为 null。
    /// </summary>
    public string OriginalParameterType { get; set; }

    /// <summary>
    /// 指示参数是否经过映射元数据补全或重绑定。
    /// </summary>
    public bool IsMetadataBound { get; set; }

    /// <summary>
    /// 当前操作的参数诊断项；默认返回空集合。
    /// </summary>
    public IReadOnlyList<SqlParameterDiagnosticInfo> Items { get; set; } = Array.Empty<SqlParameterDiagnosticInfo>();
}