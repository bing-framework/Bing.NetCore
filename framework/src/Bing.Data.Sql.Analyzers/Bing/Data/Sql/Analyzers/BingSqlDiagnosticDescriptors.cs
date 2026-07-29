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
}