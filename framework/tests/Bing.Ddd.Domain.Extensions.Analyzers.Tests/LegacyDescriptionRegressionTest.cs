using System.Linq;
using Bing.Domain.ChangeTracking;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

public sealed partial class ChangeGraphSourceGeneratorTest
{
    /// <summary>无法静态读取的 DescriptionAttribute 派生特性必须给出定位诊断。</summary>
    [Fact]
    public void DerivedDescriptionAttribute_ReportsLocatedDiagnostic()
    {
        var result = RunGenerator("""
            using System;
            using System.ComponentModel;
            using Bing.Domain.Entities;
            [AttributeUsage(AttributeTargets.Property)]
            public sealed class LabelAttribute : DescriptionAttribute
            {
                public LabelAttribute() : base("运行时描述") { }
            }
            public partial class Item : DomainObjectBase<Item>
            {
                [Label] public string Name { get; set; }
                public override Bing.Validation.IValidationResult Validate() => null;
            }
            """);

        var diagnostic = result.Diagnostics.FirstOrDefault(item => item.Id == "CG018");
        Assert.NotNull(diagnostic);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Contains("LabelAttribute", diagnostic.GetMessage());
    }

    /// <summary>资源类型继承的可访问静态字符串属性可生成和实际调用。</summary>
    [Fact]
    public void InheritedPublicStaticDisplayResource_GeneratesAccessibleMemberAccess()
    {
        var result = RunGenerator("""
            using System.Linq;
            using System.ComponentModel.DataAnnotations;
            using Bing.Domain.Entities;
            public class BaseLabels
            {
                public static string Label => "继承资源";
            }
            public class Labels : BaseLabels { }
            public partial class Item : DomainObjectBase<Item>
            {
                [Display(Name = "Label", ResourceType = typeof(Labels))]
                public string Name { get; set; }
                public override Bing.Validation.IValidationResult Validate() => null;
                public static Bing.Domain.ChangeTracking.ChangedValueDescriptor Describe()
                {
                    var before = new Item { Name = "old" };
                    var after = new Item { Name = "new" };
                    return before.GetChanges(after).Single();
                }
            }
            """);

        Assert.DoesNotContain(result.Diagnostics, item => item.Severity == DiagnosticSeverity.Error);
        Assert.True(result.EmitSucceeded);
        Assert.Contains("global::Labels.Label", result.GeneratedSource);

        var itemType = System.Reflection.Assembly.Load(result.AssemblyImage).GetType("Item", throwOnError: true);
        var change = (ChangedValueDescriptor)itemType.GetMethod("Describe").Invoke(null, null);
        Assert.Equal("Name", change.PropertyName);
        Assert.Equal("继承资源", change.Description);
        Assert.Equal("\"old\"", change.OldValue);
        Assert.Equal("\"new\"", change.NewValue);
    }

    /// <summary>派生资源类型声明的无效同名成员会隐藏基类成员并继续报错。</summary>
    [Theory]
    [InlineData("public static int Label => 1;")]
    [InlineData("public string Label => \"instance\";")]
    public void InvalidDerivedResourceMember_ReportsLocatedDiagnostic(string member)
    {
        var result = RunGenerator("""
            using System.ComponentModel.DataAnnotations;
            using Bing.Domain.Entities;
            public class BaseLabels { public static string Label => "base"; }
            public class Labels : BaseLabels
            {
            """ + member + """
            }
            public partial class Item : DomainObjectBase<Item>
            {
                [Display(Name = "Label", ResourceType = typeof(Labels))]
                public string Name { get; set; }
                public override Bing.Validation.IValidationResult Validate() => null;
            }
            """);

        var diagnostic = result.Diagnostics.FirstOrDefault(item => item.Id == "CG017");
        Assert.NotNull(diagnostic);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
    }
}
