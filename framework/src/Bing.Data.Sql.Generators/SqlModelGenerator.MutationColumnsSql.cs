using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Bing.Data.Sql.Generators;

public sealed partial class SqlModelGenerator
{
    /// <summary>可选运行时合同必须精确匹配，旧版本或同名不兼容属性不启用生成。</summary>
    private static bool HasGeneratedMutationColumnsSqlContract(Compilation compilation)
    {
        var metadata = compilation.GetTypeByMetadataName("Bing.Data.Sql.Metadata.EntityModelMetadata");
        var operation = compilation.GetTypeByMetadataName("Bing.Data.Sql.Builders.SqlOperationKind");
        var text = compilation.GetSpecialType(SpecialType.System_String);
        var list = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyList`1")?.Construct(text);
        var factory = operation == null || list == null ? null : compilation.GetTypeByMetadataName("System.Func`6")?
            .Construct(text, operation, list, compilation.GetSpecialType(SpecialType.System_Int32),
                compilation.GetSpecialType(SpecialType.System_Boolean), text);
        return factory != null && metadata?.GetMembers("GeneratedMutationColumnsSql").OfType<IPropertySymbol>()
            .Any(property => property.DeclaredAccessibility == Accessibility.Public &&
                property.SetMethod?.DeclaredAccessibility == Accessibility.Public &&
                SymbolEqualityComparer.Default.Equals(property.Type, factory)) == true;
    }

    /// <summary>按 Provider 生成有序写列的片段拼接代码，代码量随模型列数线性增长。</summary>
    private static void AppendMutationColumnsSql(StringBuilder code, ModelProjection model,
        ModelPropertyProjection[] properties, ModelPropertyProjection[] insert, ModelPropertyProjection[] update,
        ModelPropertyProjection[] conditions, bool hasKeys)
    {
        code.AppendLine("GeneratedMutationColumnsSql = static (provider, operation, properties, rowCount, returning) => {");
        code.AppendLine("if (properties == null || properties.Count == 0 || rowCount <= 0) return null;");
        code.AppendLine("switch (provider, operation) {");
        foreach (var dialect in new[]
        {
            (Key: "bing.sqlite", Open: "`", Close: "`", Prefix: "@_p_"),
            (Key: "bing.mysql", Open: "`", Close: "`", Prefix: "@_p_"),
            (Key: "bing.postgresql", Open: "\"", Close: "\"", Prefix: "@_p_"),
            (Key: "bing.sqlserver", Open: "[", Close: "]", Prefix: "@_p_"),
            (Key: "bing.oracle", Open: "\"", Close: "\"", Prefix: ":p_")
        })
        {
            if (!string.IsNullOrEmpty(model.Schema) && dialect.Key == "bing.sqlite")
                continue;
            string Quote(string value) => dialect.Open + value + dialect.Close;
            var table = Quote(model.TableName);
            if (!string.IsNullOrEmpty(model.Schema))
                table = Quote(model.Schema!) + "." + table;
            var supportsReturning = dialect.Key is "bing.sqlite" or "bing.postgresql" or "bing.sqlserver";
            var output = dialect.Key == "bing.sqlserver";
            var returnedColumns = string.Join(", ", properties.Select(property =>
                (output ? "[Inserted]." : "") + Quote(property.ColumnName) + " As " + Quote(property.Name)));

            foreach (var isInsert in new[] { true, false })
            {
                var writable = isInsert ? insert : update;
                if (writable.Length == 0 || !isInsert && !hasKeys)
                    continue;
                code.Append("case (").Append(Literal(dialect.Key))
                    .Append(", global::Bing.Data.Sql.Builders.SqlOperationKind.")
                    .Append(isInsert ? "InsertValues" : "Update").AppendLine("): {");
                if (!isInsert || dialect.Key == "bing.oracle")
                    code.AppendLine("if (rowCount != 1) return null;");
                if (!supportsReturning)
                    code.AppendLine("if (returning) return null;");
                code.Append("var sql = new global::System.Text.StringBuilder(")
                    .Append(Literal((isInsert ? "Insert Into " : "Update ") + table + (isInsert ? " (" : " Set ")))
                    .AppendLine(");");
                code.AppendLine("for (var index = 0; index < properties.Count; index++) {");
                code.AppendLine("var column = properties[index] switch {");
                foreach (var property in writable)
                    code.Append(Literal(property.Name)).Append(" => ").Append(Literal(Quote(property.ColumnName)))
                        .AppendLine(",");
                code.AppendLine("_ => null }; if (column == null) return null;");
                code.AppendLine("if (index > 0) sql.Append(\", \"); sql.Append(column);");
                if (!isInsert)
                    AppendParameter(code, " = " + dialect.Prefix, "index");
                code.AppendLine("}");
                if (isInsert)
                    code.AppendLine("sql.Append(')');");
                if (supportsReturning && output)
                    AppendReturning(code, " Output " + returnedColumns);
                if (isInsert)
                {
                    code.AppendLine("sql.Append(\" Values \");");
                    code.AppendLine("for (var row = 0; row < rowCount; row++) {");
                    code.AppendLine("if (row > 0) sql.Append(\", \"); sql.Append('(');");
                    code.AppendLine("for (var index = 0; index < properties.Count; index++) {");
                    code.AppendLine("if (index > 0) sql.Append(\", \");");
                    AppendParameter(code, dialect.Prefix, "row * properties.Count + index");
                    code.AppendLine("} sql.Append(')'); }");
                }
                else
                {
                    for (var index = 0; index < conditions.Length; index++)
                        AppendParameter(code, (index == 0 ? " Where " : " And ") +
                            Quote(conditions[index].ColumnName) + " = " + dialect.Prefix,
                            "properties.Count + " + index);
                }
                if (supportsReturning && !output)
                    AppendReturning(code, " Returning " + returnedColumns);
                code.AppendLine("return sql.ToString(); }");
            }
        }
        code.AppendLine("default: return null; } },");
    }

    /// <summary>追加固定参数前缀及按不变区域性格式化的槽位序号。</summary>
    private static void AppendParameter(StringBuilder code, string prefix, string index) => code
        .Append("sql.Append(").Append(Literal(prefix)).Append(").Append((").Append(index)
        .AppendLine(").ToString(global::System.Globalization.CultureInfo.InvariantCulture));");

    /// <summary>由生成分支决定整行返回片段在语句中的位置。</summary>
    private static void AppendReturning(StringBuilder code, string sql) => code
        .Append("if (returning) sql.Append(").Append(Literal(sql)).AppendLine(");");
}
