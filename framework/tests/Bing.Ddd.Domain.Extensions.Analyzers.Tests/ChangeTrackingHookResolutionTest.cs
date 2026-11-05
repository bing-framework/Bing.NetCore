using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

/// <summary>验证生成器只将领域对象真正的 AddChanges 重写视为手写跟踪规则。</summary>
public sealed partial class ChangeGraphSourceGeneratorTest
{
    /// <summary>无关的重载即使被重写，也不能阻止派生对象生成自己的变更跟踪。</summary>
    [Theory]
    [InlineData("protected virtual void AddChanges(int value) { }", "protected override void AddChanges(int value) { }")]
    [InlineData("protected virtual void AddChanges<T>(Root other) { }", "protected override void AddChanges<T>(Root other) { }")]
    [InlineData("protected virtual void AddChanges(ref Root other) { }", "protected override void AddChanges(ref Root other) { }")]
    public void Generate_WhenUnrelatedOverloadIsOverridden_ShouldGenerateAndTrackDerivedChanges(
        string baseOverload, string derivedOverload)
    {
        var result = RunGenerator($$"""
            using System.ComponentModel;
            using Bing.Domain.Entities;

            public abstract partial class RootBase : DomainObjectBase<Root>
            {
                {{baseOverload}}
            }

            public partial class Root : RootBase
            {
                [Description("名称")] public string Name { get; set; }
                {{derivedOverload}}
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        var generated = GetGeneratedAddChangesMethod(result, "Root").ToFullString();
        Assert.Contains("AddChange(nameof(Name), \"名称\", this.Name, other.Name);", generated);

        var change = Assert.Single(CompareGenerated(result, "Root",
            new Dictionary<string, object> { ["Name"] = "old" },
            new Dictionary<string, object> { ["Name"] = "new" }));
        Assert.Equal(("Name", "名称", "\"old\"", "\"new\""),
            (change.PropertyName, change.Description, change.OldValue, change.NewValue));
    }

    /// <summary>非冲突的普通、泛型和按引用重载都不能被误报为 hook 冲突。</summary>
    [Theory]
    [InlineData("protected void AddChanges() { }")]
    [InlineData("protected void AddChanges(int value) { }")]
    [InlineData("protected void AddChanges<T>(Root other) { }")]
    [InlineData("protected void AddChanges(ref Root other) { }")]
    public void Generate_WhenUnrelatedMethodIsDeclared_ShouldGenerateAndTrackChanges(string overload)
    {
        var result = RunGenerator($$"""
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public string Name { get; set; }
                {{overload}}
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.Single(GetGeneratedAddChangesMethods(result, "Root"));
        var change = Assert.Single(CompareGenerated(result, "Root",
            new Dictionary<string, object> { ["Name"] = "old" },
            new Dictionary<string, object> { ["Name"] = "new" }));
        Assert.Equal(("Name", "Name", "\"old\"", "\"new\""),
            (change.PropertyName, change.Description, change.OldValue, change.NewValue));
    }

    /// <summary>真正的手写 hook 与其他重载共存时，仍只使用手写规则。</summary>
    [Fact]
    public void Generate_WhenActualManualHookAndOtherOverrideCoexist_ShouldRespectManualHook()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            public abstract partial class RootBase : DomainObjectBase<Root>
            {
                protected virtual void AddChanges(int value) { }
            }
            public partial class Root : RootBase
            {
                public string Name { get; set; }
                public string Omitted { get; set; }
                protected override void AddChanges(int value) { }
                protected override void AddChanges(Root other) =>
                    AddChange(nameof(Name), "manual", Name, other.Name);
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.Empty(GetGeneratedAddChangesMethods(result, "Root"));
        var change = Assert.Single(CompareGenerated(result, "Root",
            new Dictionary<string, object> { ["Name"] = "old", ["Omitted"] = "old" },
            new Dictionary<string, object> { ["Name"] = "new", ["Omitted"] = "new" }));
        Assert.Equal(("Name", "manual", "\"old\"", "\"new\""),
            (change.PropertyName, change.Description, change.OldValue, change.NewValue));
    }

    /// <summary>中间基类覆盖真实 hook 时，派生类仍可生成自己的比较规则。</summary>
    [Fact]
    public void Generate_WhenBaseOverridesDomainHook_ShouldGenerateDerivedHook()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            public abstract partial class RootBase : DomainObjectBase<Root>
            {
                protected override void AddChanges(Root other) { }
            }
            public partial class Root : RootBase
            {
                public string Name { get; set; }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.Single(GetGeneratedAddChangesMethods(result, "Root"));
        var change = Assert.Single(CompareGenerated(result, "Root",
            new Dictionary<string, object> { ["Name"] = "old" },
            new Dictionary<string, object> { ["Name"] = "new" }));
        Assert.Equal(("Name", "Name", "\"old\"", "\"new\""),
            (change.PropertyName, change.Description, change.OldValue, change.NewValue));
    }

    /// <summary>同签名但不属于领域 hook 的重写必须诊断，不能假装手写规则有效。</summary>
    [Fact]
    public void Generate_WhenOverrideTargetsHiddenHook_ShouldReportConflict()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            public abstract partial class RootBase : DomainObjectBase<Root>
            {
                protected new virtual void AddChanges(Root other) { }
            }
            public partial class Root : RootBase
            {
                public string Name { get; set; }
                protected override void AddChanges(Root other) { }
            }
            """);

        var conflicts = result.Diagnostics.Where(diagnostic => diagnostic.Id == "CG020").ToArray();
        Assert.Contains(conflicts, diagnostic => diagnostic.GetMessage().Contains("RootBase"));
        Assert.Contains(conflicts, diagnostic => diagnostic.GetMessage().Contains("Root”"));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS0111");
        Assert.Empty(GetGeneratedAddChangesMethods(result, "Root"));
    }

    /// <summary>同签名静态方法仍会与生成的实例方法冲突，必须发出明确诊断。</summary>
    [Fact]
    public void Generate_WhenStaticMethodHasHookSignature_ShouldReportConflict()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                protected static new void AddChanges(Root other) { }
            }
            """);

        AssertGeneratorDiagnostic(result, "CG020", DiagnosticSeverity.Error);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS0111");
        Assert.Empty(GetGeneratedAddChangesMethods(result, "Root"));
    }

    /// <summary>不同返回类型不能与生成的同签名方法共存。</summary>
    [Fact]
    public void Generate_WhenMethodDiffersOnlyInReturnType_ShouldReportConflict()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                protected new int AddChanges(Root other) => 0;
            }
            """);

        AssertGeneratorDiagnostic(result, "CG020", DiagnosticSeverity.Error);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS0111");
        Assert.Empty(GetGeneratedAddChangesMethods(result, "Root"));
    }

    /// <summary>跨程序集的隐藏方法或密封重写不能被误认为可生成的领域 hook。</summary>
    [Theory]
    [InlineData("protected new virtual void AddChanges(T other) { }")]
    [InlineData("protected sealed override void AddChanges(T other) { }")]
    public void Generate_WhenInheritedExternalHookCannotBeOverridden_ShouldReportConflict(string baseHook)
    {
        var baseTree = CSharpSyntaxTree.ParseText($$"""
            using Bing.Domain.Entities;
            using Bing.Validation;
            public abstract class ExternalBase<T> : DomainObjectBase<T>
                where T : class, IDomainObject, IVerifyModel<T>
            {
                {{baseHook}}
            }
            """);
        var compilation = CSharpCompilation.Create("ExternalHookBase", new[] { baseTree },
            CreateMetadataReferences(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var image = new MemoryStream();
        var emit = compilation.Emit(image);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        var reference = MetadataReference.CreateFromImage(image.ToArray());

        var result = RunGenerator("""
            public partial class Root : ExternalBase<Root>
            {
                public string Name { get; set; }
            }
            """, reference);

        AssertGeneratorDiagnostic(result, "CG020", DiagnosticSeverity.Error);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS0239");
        Assert.Empty(GetGeneratedAddChangesMethods(result, "Root"));
    }
}
