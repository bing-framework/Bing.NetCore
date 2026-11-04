using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Bing.Analyzers;

public sealed partial class ChangeTrackableSourceGenerator
{
    /// <summary>从真实领域基类获取虚方法的参数，避免将派生类型误作重写签名。</summary>
    private static ITypeSymbol GetChangeParameterType(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current != null; current = current.BaseType)
            if (current.OriginalDefinition.ToDisplayString() == "Bing.Domain.Entities.DomainObjectBase<TObject>")
                return current.TypeArguments[0];
        return type;
    }

    /// <summary>重写入口使用基类契约，采集仍使用当前实体的完整静态类型。</summary>
    private static void AppendComparedObject(StringBuilder source, INamedTypeSymbol type, ITypeSymbol parameterType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, parameterType))
            source.AppendLine("        var typedOther = other;");
        else
            source.Append("        var typedOther = other as ").Append(TypeName(type))
                .AppendLine(" ?? throw new global::System.InvalidOperationException(\"变更比较对象类型不匹配。\");");
    }
}
