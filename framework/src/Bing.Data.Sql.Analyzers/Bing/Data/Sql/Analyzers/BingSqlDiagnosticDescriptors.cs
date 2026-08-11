using Microsoft.CodeAnalysis;

namespace Bing.Data.Sql.Analyzers;

/// <summary>
/// Bing.Data.Sql 编译期诊断描述符。
/// </summary>
internal static class BingSqlDiagnosticDescriptors
{
    /// <summary>
    /// 插值字符串传入普通 SQL 文本入口的诊断标识。
    /// </summary>
    public const string UnsafeInterpolatedSqlId = "BINGSQL002";

    /// <summary>
    /// 插值字符串传入普通 SQL 文本入口的诊断规则。
    /// </summary>
    public static readonly DiagnosticDescriptor UnsafeInterpolatedSql = new(
        UnsafeInterpolatedSqlId,
        "插值 SQL 未参数化",
        "插值字符串传入普通 SQL string 入口会拼接值，请改用 TextInterpolated<T>() 或参数对象。",
        "Bing.Data.Sql.Security",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Interpolated SQL values must use the FormattableString entry point or an explicit parameter object.");
}