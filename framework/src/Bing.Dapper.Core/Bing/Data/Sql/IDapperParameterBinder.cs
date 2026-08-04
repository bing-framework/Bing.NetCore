namespace Bing.Data.Sql;

/// <summary>
/// Dapper 参数绑定器。
/// </summary>
/// <remarks>
/// 保留 <see cref="ISqlParameterBinder"/> 作为通用 SQL 兼容入口；Dapper 调用方可逐步迁移到此契约。
/// </remarks>
internal interface IDapperParameterBinder : ISqlParameterContextBinder
{
}