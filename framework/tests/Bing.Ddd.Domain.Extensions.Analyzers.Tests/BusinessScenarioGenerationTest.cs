using System;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

/// <summary>
/// 按业务测试场景核验领域对象 Source Generator 的源码、诊断和 hook 行为。
/// </summary>
public sealed partial class ChangeGraphSourceGeneratorTest
{
    /// <summary>GEN-01：五类业务模型均生成静态描述和四参数直接属性比较。</summary>
    [Fact]
    public void GEN01_FiveBusinessModels_ShouldEmitDescribedDirectScalarChanges()
    {
        var result = RunGenerator("""
            using System.ComponentModel;
            using Bing.Domain.Entities;

            public partial class Order : DomainObjectBase<Order>
            {
                [Description("订单状态")] public string Status { get; set; }
            }

            public partial class Product : DomainObjectBase<Product>
            {
                [Description("商品名称")] public string Name { get; set; }
            }

            public partial class Role : DomainObjectBase<Role>
            {
                [Description("角色名称")] public string Name { get; set; }
            }

            public partial class Menu : DomainObjectBase<Menu>
            {
                [Description("菜单路由")] public string Route { get; set; }
            }

            public partial class ProductBatchStock : DomainObjectBase<ProductBatchStock>
            {
                [Description("批次数量")] public int Quantity { get; set; }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.Contains("AddChange(nameof(Status), \"订单状态\", this.Status, other.Status);",
            GetGeneratedAddChangesMethod(result, "Order").ToFullString());
        Assert.Contains("AddChange(nameof(Name), \"商品名称\", this.Name, other.Name);",
            GetGeneratedAddChangesMethod(result, "Product").ToFullString());
        Assert.Contains("AddChange(nameof(Name), \"角色名称\", this.Name, other.Name);",
            GetGeneratedAddChangesMethod(result, "Role").ToFullString());
        Assert.Contains("AddChange(nameof(Route), \"菜单路由\", this.Route, other.Route);",
            GetGeneratedAddChangesMethod(result, "Menu").ToFullString());
        Assert.Contains("AddChange(nameof(Quantity), \"批次数量\", this.Quantity, other.Quantity);",
            GetGeneratedAddChangesMethod(result, "ProductBatchStock").ToFullString());

        foreach (var forbidden in new[]
                 {
                     "GetDisplayNameOrDescription", "GetPropertyValue", "GetCustomAttribute", "PropertyInfo"
                 })
            Assert.DoesNotContain(forbidden, result.GeneratedSource);
    }

    /// <summary>GEN-02：关系形状只使用固定辅助调用，不向父级传递选择器或比较桥接。</summary>
    [Fact]
    public void GEN02_ObjectCollectionAndDictionary_ShouldEmitFixedRelationshipHelpers()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.ComponentModel;
            using Bing.Domain.Entities;

            public partial class Order : DomainObjectBase<Order>
            {
                [Description("收货地址")] public Address ShippingAddress { get; set; }
                [Description("订单明细")] public List<OrderLine> Lines { get; set; }
                [Description("按商品编号映射的明细")] public Dictionary<string, OrderLine> LinesByProduct { get; set; }
            }

            public partial class Address : DomainObjectBase<Address>
            {
                public string City { get; set; }
            }

            public partial class OrderLine : DomainObjectBase<OrderLine>, IEntity
            {
                public int Id { get; set; }
                public int Quantity { get; set; }
                public object[] GetKeys() => new object[] { Id };
                public void Init() { }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        var addChanges = GetGeneratedAddChangesMethod(result, "Order").ToFullString();
        Assert.Contains(
            "AddObjectChanges(nameof(ShippingAddress), \"收货地址\", this.ShippingAddress, other.ShippingAddress);",
            addChanges);
        Assert.Contains("AddCollectionChanges(nameof(Lines), \"订单明细\", this.Lines, other.Lines);", addChanges);
        Assert.Contains(
            "AddDictionaryChanges(nameof(LinesByProduct), \"按商品编号映射的明细\", this.LinesByProduct, other.LinesByProduct);",
            addChanges);

        foreach (var forbidden in new[]
                 {
                     "KeySelector", "keySelector", "AddChanges(t =>", "GetChanges(", "ChangeGraphModel",
                     "GeneratedChangeGraphModel", "ChangeGraphTracker", "Func<", "Action<", "Expression"
                 })
            Assert.DoesNotContain(forbidden, addChanges);
    }

    /// <summary>GEN-03：合法手写 override 独占 hook，生成器不会补回手写规则遗漏的字段。</summary>
    [Fact]
    public void GEN03_ManualOverride_ShouldRemainTheOnlyAddChangesRule()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;

            public partial class OrderLine : DomainObjectBase<OrderLine>
            {
                public int Quantity { get; set; }
                public decimal UnitPrice { get; set; }

                protected override void AddChanges(OrderLine other) =>
                    AddChange(nameof(Quantity), "数量", this.Quantity, other.Quantity);

                public string[] InvokeManualHook(OrderLine other)
                {
                    AddChanges(other);
                    return ChangeTrackingContext.GetChangedValueDescriptor()
                        .Select(change => change.PropertyName).ToArray();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.Empty(GetGeneratedAddChangesMethods(result, "OrderLine"));

        var assembly = Assembly.Load(result.AssemblyImage);
        var lineType = assembly.GetType("OrderLine", throwOnError: true);
        var before = Activator.CreateInstance(lineType);
        var after = Activator.CreateInstance(lineType);
        lineType.GetProperty("Quantity").SetValue(before, 2);
        lineType.GetProperty("Quantity").SetValue(after, 5);
        lineType.GetProperty("UnitPrice").SetValue(before, 10m);
        lineType.GetProperty("UnitPrice").SetValue(after, 20m);

        var names = (string[])lineType.GetMethod("InvokeManualHook").Invoke(before, new[] { after });
        Assert.Equal(new[] { "Quantity" }, names);
    }

    /// <summary>GEN-04：忽略成员和默认审计字段的 getter 不会被生成 hook 读取。</summary>
    [Fact]
    public void GEN04_IgnoredAndAuditProperties_ShouldNotBeReadByGeneratedHook()
    {
        var result = RunGenerator("""
            using System;
            using Bing.Auditing;
            using Bing.Domain.ChangeTracking;
            using Bing.Domain.Entities;

            public partial class AuditedOrder : DomainObjectBase<AuditedOrder>,
                IHasCreationTime, IHasModificationTime, IHasDeletionTime
            {
                public string Status { get; set; }

                [ChangeIgnore]
                public string Secret { get => throw new InvalidOperationException("Secret was read."); set { } }

                [ChangeIgnore]
                public Address HiddenAddress
                {
                    get => throw new InvalidOperationException("Hidden relation was read.");
                    set { }
                }

                public DateTime? CreationTime
                {
                    get => throw new InvalidOperationException("CreationTime was read.");
                    set { }
                }

                public DateTime? LastModificationTime
                {
                    get => throw new InvalidOperationException("LastModificationTime was read.");
                    set { }
                }

                public DateTime? DeletionTime
                {
                    get => throw new InvalidOperationException("DeletionTime was read.");
                    set { }
                }

                public bool IsDeleted
                {
                    get => throw new InvalidOperationException("IsDeleted was read.");
                    set { }
                }

                public string Version
                {
                    get => throw new InvalidOperationException("Version was read.");
                    set { }
                }

                public void InvokeGeneratedHook(AuditedOrder other) => AddChanges(other);
            }

            public partial class Address : DomainObjectBase<Address>
            {
                public string City { get; set; }
            }
            """,
            MetadataReference.CreateFromFile(typeof(Bing.Data.ISoftDelete).Assembly.Location));

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        var addChanges = GetGeneratedAddChangesMethod(result, "AuditedOrder").ToFullString();
        foreach (var ignoredProperty in new[]
                 {
                     "Secret", "HiddenAddress", "CreationTime", "LastModificationTime", "DeletionTime",
                     "IsDeleted", "Version"
                 })
        {
            Assert.DoesNotContain("this." + ignoredProperty, addChanges);
            Assert.DoesNotContain("other." + ignoredProperty, addChanges);
        }

        var assembly = Assembly.Load(result.AssemblyImage);
        var orderType = assembly.GetType("AuditedOrder", throwOnError: true);
        var before = Activator.CreateInstance(orderType);
        var after = Activator.CreateInstance(orderType);
        var exception = Record.Exception(() =>
            orderType.GetMethod("InvokeGeneratedHook").Invoke(before, new[] { after }));
        Assert.Null(exception);
    }

    /// <summary>GEN-05：非 partial 领域类型报告错误诊断，且不发射该类型的代码。</summary>
    [Fact]
    public void GEN05_NonPartialEntity_ShouldReportErrorAndEmitNoGeneratedFiles()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            public class Order : DomainObjectBase<Order>
            {
                public string Number { get; set; }
            }
            """);

        AssertGeneratorDiagnostic(result, "CG001", DiagnosticSeverity.Error);
        Assert.Empty(result.GeneratedTrees);
        Assert.True(result.EmitSucceeded);
    }

    /// <summary>GEN-05：new 同名 AddChanges hook 报错并阻止重复生成。</summary>
    [Fact]
    public void GEN05_NewAddChangesHook_ShouldReportErrorAndEmitNoGeneratedFiles()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            public partial class Order : DomainObjectBase<Order>
            {
                protected new void AddChanges(Order other) { }
            }
            """);

        AssertGeneratorDiagnostic(result, "CG020", DiagnosticSeverity.Error);
        Assert.Empty(result.GeneratedTrees);
        Assert.True(result.EmitSucceeded);
    }

    /// <summary>GEN-05：普通 POCO 关系明确警告且不会生成伪装成受支持的比较调用。</summary>
    [Fact]
    public void GEN05_PocoRelationship_ShouldWarnAndOmitRelationshipHelper()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;

            public partial class Order : DomainObjectBase<Order>
            {
                public Address ShippingAddress { get; set; }
            }

            public class Address
            {
                public string City { get; set; }
            }
            """);

        AssertGeneratorDiagnostic(result, "CG006", DiagnosticSeverity.Warning);
        Assert.True(result.EmitSucceeded);
        Assert.DoesNotContain("AddObjectChanges(nameof(ShippingAddress)",
            GetGeneratedAddChangesMethod(result, "Order").ToFullString());
    }

    /// <summary>GEN-05：无身份复杂 Set 按错误处理，而不是生成空比较并声称受支持。</summary>
    [Fact]
    public void GEN05_ComplexSetWithoutIdentity_ShouldReportErrorAndOmitHelper()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using Bing.Domain.Entities;

            public partial class WarehouseStock : DomainObjectBase<WarehouseStock>
            {
                public HashSet<ProductBatch> Batches { get; set; }
            }

            public class ProductBatch
            {
                public string BatchNumber { get; set; }
            }
            """);

        AssertGeneratorDiagnostic(result, "CG019", DiagnosticSeverity.Error);
        Assert.True(result.EmitSucceeded);
        Assert.DoesNotContain("AddCollectionChanges(nameof(Batches)",
            GetGeneratedAddChangesMethod(result, "WarehouseStock").ToFullString());
    }

    /// <summary>GEN-06：override 属性按派生声明绑定一次，并生成可编译的直接读取。</summary>
    [Fact]
    public void GEN06_OverriddenProperty_ShouldBindToDerivedDeclarationOnce()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;

            public partial class PersonBase : DomainObjectBase<PersonBase>
            {
                public virtual string DisplayName { get; set; }
            }

            public partial class Person : PersonBase
            {
                public override string DisplayName { get; set; }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        var addChanges = GetGeneratedAddChangesMethod(result, "Person").ToFullString();
        const string expected = "AddChange(nameof(DisplayName), \"DisplayName\", this.DisplayName, typedOther.DisplayName);";
        Assert.Contains(expected, addChanges);
        Assert.Equal(1, addChanges.Split("AddChange(nameof(DisplayName)", StringSplitOptions.None).Length - 1);
    }

    /// <summary>GEN-06：new 隐藏属性绑定派生属性本身，不重复采集同名基类属性。</summary>
    [Fact]
    public void GEN06_NewHiddenProperty_ShouldBindToDerivedDeclarationOnce()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;

            public partial class HiddenBase : DomainObjectBase<HiddenBase>
            {
                public string Value { get; set; }
            }

            public partial class HiddenDerived : HiddenBase
            {
                public new int Value { get; set; }

                public string InvokeGeneratedHook(HiddenBase other)
                {
                    AddChanges(other);
                    var change = ChangeTrackingContext.GetChangedValueDescriptor().Single();
                    return change.OldValue + "->" + change.NewValue;
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        var addChanges = GetGeneratedAddChangesMethod(result, "HiddenDerived").ToFullString();
        const string expected = "AddChange(nameof(Value), \"Value\", this.Value, typedOther.Value);";
        Assert.Contains(expected, addChanges);
        Assert.Equal(1, addChanges.Split("AddChange(nameof(Value)", StringSplitOptions.None).Length - 1);

        var assembly = Assembly.Load(result.AssemblyImage);
        var baseType = assembly.GetType("HiddenBase", throwOnError: true);
        var derivedType = assembly.GetType("HiddenDerived", throwOnError: true);
        var before = Activator.CreateInstance(derivedType);
        var after = Activator.CreateInstance(derivedType);
        baseType.GetProperty("Value").SetValue(before, "old-base");
        baseType.GetProperty("Value").SetValue(after, "new-base");
        derivedType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SetValue(before, 1);
        derivedType.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SetValue(after, 2);

        var actual = (string)derivedType.GetMethod("InvokeGeneratedHook").Invoke(before, new[] { after });
        Assert.Equal("1->2", actual);
    }

    /// <summary>GEN-06：泛型父类的继承成员在闭合实体中准确绑定。</summary>
    [Fact]
    public void GEN06_GenericParent_ShouldBindInheritedMemberInClosedEntity()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Validation;

            public abstract partial class GenericBase<TSelf> : DomainObjectBase<TSelf>
                where TSelf : class, IDomainObject, IVerifyModel<TSelf>
            {
                public string TenantCode { get; set; }
            }

            public partial class GenericChild : GenericBase<GenericChild>
            {
                public int Quantity { get; set; }

                public string[] InvokeGeneratedHook(GenericChild other)
                {
                    AddChanges(other);
                    return ChangeTrackingContext.GetChangedValueDescriptor()
                        .Select(change => change.PropertyName).ToArray();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        var addChanges = GetGeneratedAddChangesMethod(result, "GenericChild").ToFullString();
        Assert.Contains("AddChange(nameof(TenantCode), \"TenantCode\", this.TenantCode, other.TenantCode);",
            addChanges);
        Assert.Equal(1, addChanges.Split("AddChange(nameof(TenantCode)", StringSplitOptions.None).Length - 1);

        var assembly = Assembly.Load(result.AssemblyImage);
        var childType = assembly.GetType("GenericChild", throwOnError: true);
        var before = Activator.CreateInstance(childType);
        var after = Activator.CreateInstance(childType);
        childType.GetProperty("TenantCode").SetValue(before, "before");
        childType.GetProperty("TenantCode").SetValue(after, "after");

        var names = (string[])childType.GetMethod("InvokeGeneratedHook").Invoke(before, new[] { after });
        Assert.Equal(new[] { "TenantCode" }, names);
    }

    /// <summary>GEN-07：完整检查领域类型生成文件，禁止残留图模型和反射桥接产物。</summary>
    [Fact]
    public void GEN07_AllDomainGeneratedFiles_ShouldNotContainGraphOrReflectionBridges()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;

            public partial class Order : DomainObjectBase<Order>
            {
                public string OrderNumber { get; set; }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        var generatedFiles = result.GeneratedTrees.Select(tree => tree.GetText().ToString()).ToArray();
        Assert.NotEmpty(generatedFiles);

        var forbiddenArtifacts = new[]
        {
            "GeneratedChangeGraphModel", "ChangeGraphModel", "CaptureChanges", "__ChangeGraph",
            "ChangeGraphWriter", "ChangeGraphTracker", "ChangeGraphKey", "KeySelector",
            "System.Reflection", "PropertyInfo", "GetProperties(", "GetPropertyValue",
            "GetDisplayNameOrDescription", "GetCustomAttribute", "Expression", ".Compile(", "dynamic "
        };
        foreach (var generatedFile in generatedFiles)
        foreach (var forbidden in forbiddenArtifacts)
            Assert.DoesNotContain(forbidden, generatedFile);
    }

    /// <summary>GEN-08：接口声明的同步 IEnumerable 直接发射 AddCollectionChanges。</summary>
    [Fact]
    public void GEN08_InterfaceEnumerable_ShouldEmitCollectionChangesWithoutGraphDiagnostics()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using Bing.Domain.ChangeTracking;
            using Bing.Domain.Entities;
            using Bing.Validation;

            public interface ITrackableLine : IChangeTrackable { }

            public partial class InterfaceSequenceRoot : DomainObjectBase<InterfaceSequenceRoot>
            {
                public IEnumerable<ITrackableLine> Lines { get; set; }
                public override IValidationResult Validate() => null;
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CG006" || diagnostic.Id == "CG011");
        var addChanges = GetGeneratedAddChangesMethod(result, "InterfaceSequenceRoot").ToFullString();
        Assert.Contains("AddCollectionChanges(nameof(Lines)", addChanges);
        Assert.DoesNotContain("ChangeGraph", result.GeneratedSource);
        Assert.DoesNotContain("CaptureChanges", result.GeneratedSource);
        Assert.DoesNotContain(".ChangeGraph.g.cs", string.Join("\n", result.GeneratedTrees.Select(tree => tree.FilePath)));
    }

    /// <summary>GEN-09：三层容器静态展开并保留字典、Set 的自身比较器。</summary>
    [Fact]
    public void GEN09_ThreeLevelContainers_ShouldEmitTypedHelpersAndPreserveComparers()
    {
        var result = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Validation;

            public partial class NestedContainerRoot : DomainObjectBase<NestedContainerRoot>
            {
                public List<Dictionary<string, HashSet<string>>> Matrix { get; set; }
                public override IValidationResult Validate() => null;

                public static string[] Compare()
                {
                    var before = new NestedContainerRoot
                    {
                        Matrix = new List<Dictionary<string, HashSet<string>>>
                        {
                            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["Main"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "same" }
                            }
                        }
                    };
                    var after = new NestedContainerRoot
                    {
                        Matrix = new List<Dictionary<string, HashSet<string>>>
                        {
                            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["main"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SAME", "new" }
                            }
                        }
                    };
                    return before.GetChanges(after).Select(change => change.PropertyName).ToArray();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.Contains("GetNestedCollectionPairs<", result.GeneratedSource);
        Assert.Contains("GetNestedDictionaryPairs<", result.GeneratedSource);
        Assert.Contains("AddCollectionChanges(pair.Path", result.GeneratedSource);
        Assert.Equal(2, result.GeneratedSource.Split("private static void __BingCompareNestedChanges",
            StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("GeneratedChangeGraphModel", result.GeneratedSource);
        Assert.DoesNotContain("System.Reflection", result.GeneratedSource);

        var assembly = Assembly.Load(result.AssemblyImage);
        var rootType = assembly.GetType("NestedContainerRoot", throwOnError: true);
        var paths = (string[])rootType.GetMethod("Compare").Invoke(null, null);
        Assert.Equal(new[] { "Matrix[0]{Main}[\"new\"]" }, paths);
    }

    /// <summary>GEN-10：实体自行提供 decimal 身份，生成器不再限制 IKey 的键类型。</summary>
    [Fact]
    public void GEN10_EntityDecimalKey_ShouldMatchByKeyWithoutTypeDiagnostics()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Validation;

            public partial class DecimalKeyLine : DomainObjectBase<DecimalKeyLine>, IEntity
            {
                public decimal Id { get; set; }
                public int Quantity { get; set; }
                public object[] GetKeys() => new object[] { Id };
                public void Init() { }
                public override IValidationResult Validate() => null;
            }

            public partial class DecimalKeyOrder : DomainObjectBase<DecimalKeyOrder>
            {
                public IEnumerable<DecimalKeyLine> Lines { get; set; }
                public override IValidationResult Validate() => null;
            }

            public static class Check
            {
                public static string Run() => string.Join("|", new DecimalKeyOrder
                {
                    Lines = new[] { new DecimalKeyLine { Id = 1.5m, Quantity = 2 }, new DecimalKeyLine { Id = 2.5m, Quantity = 4 } }
                }.GetChanges(new DecimalKeyOrder
                {
                    Lines = new[] { new DecimalKeyLine { Id = 2.5m, Quantity = 4 }, new DecimalKeyLine { Id = 1.5m, Quantity = 3 } }
                }).Select(change => change.PropertyName + ":" + change.OldValue + "->" + change.NewValue));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "CG019" or "CG022");
        Assert.Contains("AddCollectionChanges(nameof(Lines)",
            GetGeneratedAddChangesMethod(result, "DecimalKeyOrder").ToFullString());
        Assert.Equal("Lines[@(\"System.Decimal\",\"1.5\")].Quantity:2->3", RunCheck(result));
    }

    /// <summary>GEN-11：递归集合形状报告诊断，不在生成阶段无限递归。</summary>
    [Fact]
    public void GEN11_RecursiveContainer_ShouldReportUnsupportedMemberWithoutOverflow()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using Bing.Domain.Entities;
            using Bing.Validation;

            public class RecursiveSequence : List<RecursiveSequence> { }

            public partial class RecursiveSequenceRoot : DomainObjectBase<RecursiveSequenceRoot>
            {
                public RecursiveSequence Items { get; set; }
                public override IValidationResult Validate() => null;
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        AssertGeneratorDiagnostic(result, "CG006", DiagnosticSeverity.Warning);
        Assert.DoesNotContain("AddCollectionChanges", GetGeneratedAddChangesMethod(result, "RecursiveSequenceRoot").ToFullString());
    }

    /// <summary>GEN-12：只读 Set 的静态形状必须传入运行时，避免退化为按索引比较。</summary>
    [Fact]
    public void GEN12_ReadOnlySet_ShouldEmitExplicitSetSemantics()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using Bing.Domain.Entities;

            public partial class ReadOnlySetRoot : DomainObjectBase<ReadOnlySetRoot>
            {
                public IReadOnlySet<string> Tags { get; set; }
                public Dictionary<string, IReadOnlySet<string>> Groups { get; set; }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.Contains("AddCollectionChanges(nameof(Tags), \"Tags\", this.Tags, other.Tags, true);",
            GetGeneratedAddChangesMethod(result, "ReadOnlySetRoot").ToFullString());
        Assert.Contains("AddCollectionChanges(pair.Path, description, pair.OldValue, pair.NewValue, true);",
            result.GeneratedSource);
    }

    /// <summary>取得指定领域类型的唯一生成 AddChanges 方法。</summary>
    private static MethodDeclarationSyntax GetGeneratedAddChangesMethod(GeneratorResult result, string typeName) =>
        Assert.Single(GetGeneratedAddChangesMethods(result, typeName));

    /// <summary>取得指定领域类型生成的 AddChanges 方法集合。</summary>
    private static MethodDeclarationSyntax[] GetGeneratedAddChangesMethods(GeneratorResult result, string typeName) =>
        result.GeneratedTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Identifier.ValueText == "AddChanges" &&
                             method.ParameterList.Parameters.Count == 1 &&
                             method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()
                                 ?.Identifier.ValueText == typeName)
            .ToArray();

    /// <summary>断言生成器诊断的编号、严重度和源码位置。</summary>
    private static Diagnostic AssertGeneratorDiagnostic(
        GeneratorResult result, string id, DiagnosticSeverity severity)
    {
        // RunGenerator 合并 driver 与 runResult 诊断，同一生成器诊断可能各出现一次。
        var diagnostics = result.Diagnostics.Where(item => item.Id == id).ToArray();
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal(severity, diagnostic.Severity);
            Assert.True(diagnostic.Location.IsInSource);
        });
        return diagnostics[0];
    }
}

