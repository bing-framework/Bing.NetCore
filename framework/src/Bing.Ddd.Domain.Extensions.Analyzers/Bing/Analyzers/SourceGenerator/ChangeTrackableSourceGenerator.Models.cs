using Microsoft.CodeAnalysis;

namespace Bing.Analyzers;

/// <summary>
/// 描述逐成员 AddChanges 生成所需的类型形状。
/// </summary>
public sealed partial class ChangeTrackableSourceGenerator
{
    /// <summary>标识直接 AddChanges 成员的静态形状。</summary>
    private enum AddChangesShapeKind
    {
        Unsupported,
        Scalar,
        Object,
        Sequence,
        Set,
        Dictionary
    }

    /// <summary>描述直接 AddChanges 与嵌套容器发射所需的递归静态形状。</summary>
    private sealed class AddChangesTypeShape
    {
        internal AddChangesTypeShape(ITypeSymbol type, AddChangesShapeKind kind,
            ITypeSymbol keyType = null, AddChangesTypeShape childShape = null)
        {
            Type = type;
            Kind = kind;
            KeyType = keyType;
            ChildShape = childShape;
        }

        internal ITypeSymbol Type { get; }
        internal AddChangesShapeKind Kind { get; }
        internal ITypeSymbol KeyType { get; }
        internal AddChangesTypeShape ChildShape { get; }
        internal bool IsContainer => Kind == AddChangesShapeKind.Sequence ||
                                     Kind == AddChangesShapeKind.Set ||
                                     Kind == AddChangesShapeKind.Dictionary;
        internal bool HasNestedContainer => IsContainer && ChildShape?.IsContainer == true;
    }

    /// <summary>保存一个自动生成的成员比较调用及其嵌套辅助入口。</summary>
    private sealed class AddChangesMemberInfo
    {
        internal AddChangesMemberInfo(IPropertySymbol property, AddChangesTypeShape shape)
        {
            Property = property;
            Shape = shape;
        }

        internal IPropertySymbol Property { get; }
        internal AddChangesTypeShape Shape { get; }
        internal NestedChangeHelperInfo NestedHelper { get; set; }
    }

    /// <summary>描述一个静态递归的嵌套容器辅助方法。</summary>
    private sealed class NestedChangeHelperInfo
    {
        internal NestedChangeHelperInfo(AddChangesTypeShape shape, string methodName,
            NestedChangeHelperInfo childHelper)
        {
            Shape = shape;
            MethodName = methodName;
            ChildHelper = childHelper;
        }

        internal AddChangesTypeShape Shape { get; }
        internal string MethodName { get; }
        internal NestedChangeHelperInfo ChildHelper { get; }
    }

}
