using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 单次 SQL 执行的不可变准备快照。
/// </summary>
/// <remarks>
/// 同时保留调用方参数源和已绑定的 Dapper 参数，确保执行、Trace 与诊断共享同一份准备结果。
/// </remarks>
internal sealed class SqlPreparedCommand
{
    /// <summary>
    /// 初始化一个<see cref="SqlPreparedCommand"/>类型的实例。
    /// </summary>
    /// <param name="sql">待执行的 SQL 文本。</param>
    /// <param name="parameterSource">调用方提供的原始参数源。</param>
    /// <param name="dapperParameters">本次执行绑定后的 Dapper 参数。</param>
    /// <param name="builder">生成 SQL 的 Builder。</param>
    /// <param name="debugSql">与 SQL 使用同一快照生成的调试 SQL。</param>
    public SqlPreparedCommand(string sql, object parameterSource, object dapperParameters, ISqlBuilder builder = null,
        string debugSql = null)
    {
        Sql = sql;
        ParameterSource = parameterSource;
        DapperParameters = dapperParameters;
        Builder = builder;
        DebugSql = debugSql ?? sql;
    }

    /// <summary>
    /// 待执行的 SQL 文本。
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 调用方提供的原始参数源。
    /// </summary>
    public object ParameterSource { get; }

    /// <summary>
    /// 本次执行绑定后的 Dapper 参数。
    /// </summary>
    public object DapperParameters { get; }

    /// <summary>
    /// 生成 SQL 的 Builder；原生 SQL 时为 null。
    /// </summary>
    public ISqlBuilder Builder { get; }

    /// <summary>
    /// 与 SQL 使用同一快照生成的调试 SQL。
    /// </summary>
    public string DebugSql { get; }

    /// <summary>
    /// 是否由 SQL Builder 生成。
    /// </summary>
    public bool IsBuilderCommand => Builder != null;
}