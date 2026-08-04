using Microsoft.CodeAnalysis;

namespace Bing.Data.Sql.Analyzers;

/// <summary>
/// Bing.Data.Sql 编译期诊断描述符。
/// </summary>
internal static class BingSqlDiagnosticDescriptors
{
    /// <summary>
    /// Count 单个位置字符串参数语义不明确的诊断标识。
    /// </summary>
    public const string AmbiguousCountId = "BINGSQL001";

    /// <summary>
    /// Count 单个位置字符串参数语义不明确的诊断规则。
    /// </summary>
    public static readonly DiagnosticDescriptor AmbiguousCount = new(
        AmbiguousCountId,
        "Count 字符串参数语义不明确",
        "Count 仅传递一个位置字符串参数时语义不明确，请使用命名参数 column: 或 alias:。",
        "Bing.Data.Sql.Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The unified Count API requires a named column or alias argument.");

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
        "插值字符串传入普通 Sql string 入口会拼接值，请改用 SqlInterpolated<T>() 或参数对象。",
        "Bing.Data.Sql.Security",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Interpolated SQL values must use the FormattableString entry point or an explicit parameter object.");
}