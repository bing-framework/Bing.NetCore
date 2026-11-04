using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Bing.Analyzers;
using Bing.Domain.ChangeTracking;
using Bing.Domain.Entities;
using Bing.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

/// <summary>
/// 验证旧版 AddChanges 入口生成直接、静态的成员变更记录。
/// </summary>
public sealed class LegacyDirectChangeGenerationTest
{
    /// <summary>错误资源配置在编译期失败，不能把资源键伪装成有效描述。</summary>
    [Theory]
    [InlineData("public static string Label { private get; set; }")]
    [InlineData("public string Label => \"instance\";")]
    [InlineData("public static int Label => 1;")]
    [InlineData("public static string Missing => \"missing\";")]
    public void InvalidResource_ReportsLocatedDiagnostic(string member)
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            using System.ComponentModel.DataAnnotations;
            public class Resources {
            """ + member + """
            }
            public partial class Legacy : DomainObjectBase<Legacy> {
                [Display(Name="Label", ResourceType=typeof(Resources))]
                public string Name { get; set; }
            }
            """);
        Assert.Contains(result.Diagnostics, d => d.Id == "CG017" && d.Location.IsInSource);
        Assert.False(result.EmitSucceeded && !result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
    }

    /// <summary>
    /// 验证生成代码使用直接属性读取并以编译期元数据写入描述。
    /// </summary>
    [Fact]
    public void GenerateLegacyAddChanges_UsesDirectReadsAndStaticDescriptions()
    {
        var result = RunGenerator();

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.True(result.EmitSucceeded);
        Assert.Contains("AddChange(nameof(Plain), \"Plain\", this.Plain, other.Plain);", result.GeneratedSource);
        Assert.Contains("AddChange(nameof(DisplayOnly), \"显示名称\", this.DisplayOnly, other.DisplayOnly);", result.GeneratedSource);
        Assert.Contains("AddChange(nameof(Both), \"Display 优先\", this.Both, other.Both);", result.GeneratedSource);
        Assert.Contains("AddChange(nameof(DescriptionOnly), \"属性说明\", this.DescriptionOnly, other.DescriptionOnly);", result.GeneratedSource);
        Assert.Contains("AddChange(nameof(Localized), global::DisplayResources.Localized, this.Localized, other.Localized);", result.GeneratedSource);
        Assert.Contains("AddChange(nameof(Escaped), ", result.GeneratedSource);
        Assert.DoesNotContain("AddChange(t =>", result.GeneratedSource);
        Assert.DoesNotContain("GetDisplayNameOrDescription", result.GeneratedSource);
        Assert.DoesNotContain("GetPropertyValue", result.GeneratedSource);
        Assert.DoesNotContain("PropertyInfo", result.GeneratedSource);
    }

    /// <summary>
    /// 验证生成程序集能够执行，并保留显示描述、资源描述和属性名回退。
    /// </summary>
    [Fact]
    public void GeneratedAddChanges_WhenExecuted_UsesResolvedDescriptionsAndValues()
    {
        var result = RunGenerator();
        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.True(result.EmitSucceeded);

        var assembly = Assembly.Load(result.AssemblyImage);
        var legacyType = assembly.GetType("Legacy", throwOnError: true);
        var changes = (ChangedValueDescriptorCollection)legacyType.GetMethod("Compare").Invoke(null, null);

        Assert.Equal(
            new[] { "Both", "DescriptionOnly", "DisplayOnly", "Escaped", "Localized", "Plain" },
            changes.Select(change => change.PropertyName));
        Assert.Equal(
            new[] { "Display 优先", "属性说明", "显示名称", "双引号 \" 和反斜杠 \\ ", "本地化显示", "Plain" },
            changes.Select(change => change.Description));
        Assert.All(changes, change =>
        {
            Assert.Equal("\"old\"", change.OldValue);
            Assert.Equal("\"new\"", change.NewValue);
        });
    }

    /// <summary>
    /// 运行领域源生成器并编译其实际生成结果。
    /// </summary>
    /// <returns>生成代码、诊断和程序集映像。</returns>
    private static GeneratorResult RunGenerator(string source = null)
    {
        const string defaultSource = """
            using System.ComponentModel;
            using System.ComponentModel.DataAnnotations;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            using Bing.Validation;

            public static class DisplayResources
            {
                public static string Localized => "本地化显示";
            }

            public partial class Legacy : DomainObjectBase<Legacy>
            {
                public string Plain { get; set; }
                [Display(Name = "显示名称")]
                public string DisplayOnly { get; set; }
                [Display(Name = "Display 优先")]
                [Description("不应优先")]
                public string Both { get; set; }
                [Description("属性说明")]
                public string DescriptionOnly { get; set; }
                [Display(Name = "Localized", ResourceType = typeof(DisplayResources))]
                public string Localized { get; set; }
                [Description("双引号 \" 和反斜杠 \\ ")]
                public string Escaped { get; set; }
                public string Version { get; set; }
                public bool IsDeleted { get; set; }

                public override IValidationResult Validate() => null;

                public static ChangedValueDescriptorCollection Compare()
                {
                    var before = new Legacy { Plain = "old", DisplayOnly = "old", Both = "old", DescriptionOnly = "old", Localized = "old", Escaped = "old" };
                    var after = new Legacy { Plain = "new", DisplayOnly = "new", Both = "new", DescriptionOnly = "new", Localized = "new", Escaped = "new" };
                    return before.GetChanges(after);
                }
            }
            """;

        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source ?? defaultSource, parseOptions, "LegacyFixture.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(System.IO.Path.PathSeparator)
            .Concat(new[]
            {
                typeof(Bing.Domain.Entities.DomainObjectBase).Assembly.Location,
                typeof(ChangeTrackableSourceGenerator).Assembly.Location,
                typeof(IValidationResult).Assembly.Location
            })
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("LegacyChangeFixture", new[] { syntaxTree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[] { new ChangeTrackableSourceGenerator() }, parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var driverDiagnostics);
        var generatedSource = string.Join(System.Environment.NewLine,
            driver.GetRunResult().Results.SelectMany(generator => generator.GeneratedSources)
                .Select(generated => generated.SourceText.ToString()));
        using var output = new System.IO.MemoryStream();
        var emit = updatedCompilation.Emit(output);
        var diagnostics = driverDiagnostics
            .Concat(driver.GetRunResult().Diagnostics)
            .Concat(updatedCompilation.GetDiagnostics())
            .Concat(emit.Diagnostics)
            .ToImmutableArray();
        return new GeneratorResult(generatedSource, diagnostics, emit.Success, output.ToArray());
    }

    /// <summary>
    /// 封装源生成、编译及诊断结果。
    /// </summary>
    private sealed record GeneratorResult(string GeneratedSource, ImmutableArray<Diagnostic> Diagnostics,
        bool EmitSucceeded, byte[] AssemblyImage);
}
