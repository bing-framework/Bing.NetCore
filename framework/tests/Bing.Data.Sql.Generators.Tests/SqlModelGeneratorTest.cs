using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Bing.Data.Sql.Generated;
using Bing.Data.Sql.Generators;
using Bing.Data.Sql.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

/// <summary>
/// 验证显式 SQL 模型生成、注册及属性访问器。
/// </summary>
public sealed partial class SqlModelGeneratorTest
{
    /// <summary>直接执行每个强类型读取分支，校验可空赋值及存储类型不匹配时的无副作用回退。</summary>
    [Theory]
    [InlineData("int")]
    [InlineData("long")]
    [InlineData("string")]
    [InlineData("double")]
    [InlineData("float")]
    [InlineData("decimal")]
    [InlineData("bool")]
    [InlineData("short")]
    [InlineData("byte")]
    [InlineData("System.DateTime")]
    [InlineData("System.Guid")]
    public void Generate_ResultReader_ShouldExecuteEverySupportedTypedGetter(string typeName)
    {
        object value = typeName switch
        {
            "int" => 42, "long" => 43L, "string" => "typed", "double" => 1.25d,
            "float" => 2.5f, "decimal" => 3.75m, "bool" => true, "short" => (short)44,
            "byte" => (byte)45, "System.DateTime" => new DateTime(2026, 11, 7),
            _ => Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")
        };
        foreach (var nullable in typeName == "string" ? new[] { false } : new[] { false, true })
        {
            var result = RunGenerator("using Bing.Data.Sql.Generated; public sealed class TypedValue { public " +
                typeName + (nullable ? "?" : "") + " Value {get;set;} } " +
                "[SqlResult(typeof(TypedValue))] public static partial class TypedValueContext { }");
            AssertNoCompilationErrors(result);
            var assembly = Assembly.Load(result.AssemblyImage);
            var model = Assert.Single((EntityModelMetadata[])assembly.GetType("TypedValueContext")!
                .GetMethod("CreateModels")!.Invoke(null, null)!);
            var instance = model.GeneratedFactory!();
            var assign = model.Properties["Value"].GeneratedReaderSetter;
            Assert.NotNull(assign);
            using var table = new System.Data.DataTable();
            table.Columns.Add("Value", value.GetType());
            table.Columns.Add("Different", typeof(byte[]));
            table.Rows.Add(value, new byte[] { 1 });
            using var reader = table.CreateDataReader();
            Assert.True(reader.Read());
            Assert.True(assign!(instance, reader, 0));
            Assert.Equal(value, instance.GetType().GetProperty("Value")!.GetValue(instance));
            Assert.False(assign(instance, reader, 1));
            Assert.Equal(value, instance.GetType().GetProperty("Value")!.GetValue(instance));
        }
    }

    /// <summary>生成的读取赋值器直接写入属性，不经过数值 object setter。</summary>
    [Fact]
    public void Generate_ResultReader_ShouldAssignTypedValuesAndDeclineDifferentStorageTypes()
    {
        var result = RunGenerator("""
            using Bing.Data.Sql.Generated;
            public sealed class TypedResult { public int Id { get; set; } public long? Count { get; set; } }
            [SqlResult(typeof(TypedResult))] public static partial class TypedContext { }
            """);
        AssertNoCompilationErrors(result);
        Assert.Contains("GetInt32", result.GeneratedSource, StringComparison.Ordinal);
        var assembly = Assembly.Load(result.AssemblyImage);
        var models = (EntityModelMetadata[])assembly.GetType("TypedContext")!.GetMethod("CreateModels")!.Invoke(null, null)!;
        var model = Assert.Single(models);
        var instance = model.GeneratedFactory!();
        var descriptor = typeof(EntityPropertyMetadata).GetProperty("GeneratedReaderSetter");
        Assert.NotNull(descriptor);
        var assign = (Func<object, System.Data.Common.DbDataReader, int, bool>)descriptor!.GetValue(model.Properties["Id"])!;
        using var table = new System.Data.DataTable();
        table.Columns.Add("Id", typeof(long)); table.Columns.Add("text", typeof(string));
        table.Rows.Add(42L, "43");
        table.Rows.Add((long)int.MaxValue + 1, "overflow");
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        Assert.True(assign(instance, reader, 0));
        Assert.Equal(42, instance.GetType().GetProperty("Id")!.GetValue(instance));
        Assert.False(assign(instance, reader, 1));
        Assert.Equal(42, instance.GetType().GetProperty("Id")!.GetValue(instance));
        var nullableAssign = (Func<object, System.Data.Common.DbDataReader, int, bool>)descriptor.GetValue(model.Properties["Count"])!;
        Assert.True(nullableAssign(instance, reader, 0));
        Assert.Equal(42L, instance.GetType().GetProperty("Count")!.GetValue(instance));
        Assert.True(reader.Read());
        Assert.Throws<OverflowException>(() => assign(instance, reader, 0));
        Assert.Equal(42, instance.GetType().GetProperty("Id")!.GetValue(instance));
    }

    /// <summary>
    /// 验证模型上下文可在没有查询调用点时生成可运行的注册和属性访问器。
    /// </summary>
    [Fact]
    public void Generate_WhenModelsAreExplicitlyRegistered_ShouldCompileAndInvokeAccessors()
    {
        const string source = """
            using System;
            using System.ComponentModel.DataAnnotations;
            using System.ComponentModel.DataAnnotations.Schema;
            using Bing.Data.Sql.Generated;

            namespace GeneratedModels
            {
                public class OrderBase
                {
                    [Column("base_name", TypeName = "varchar(48)"), MaxLength(48)]
                    public string Name { get; set; } = string.Empty;

                    public string PrivateWrite { get; private set; } = string.Empty;

                    public int InitOnly { get; init; }
                }

                [Table("orders", Schema = "sales")]
                public sealed class Order : OrderBase
                {
                    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
                    public int Id { get; set; }

                    [Timestamp]
                    public byte[] Version { get; set; } = Array.Empty<byte>();

                    [NotMapped]
                    public string Scratch { get; set; } = string.Empty;
                }

                public sealed class OrderSummary
                {
                    public string Label { get; set; } = string.Empty;
                }

                public sealed class OrderFilter
                {
                    public int? MinimumId { get; set; }
                }

                [SqlModel(typeof(Order))]
                [SqlResult(typeof(OrderSummary))]
                public static partial class ApplicationSqlModel
                {
                }

                [SqlParameters(typeof(OrderFilter))]
                public static partial class ApplicationSqlModel
                {
                }
            }
            """;

        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.Equal(1, result.GeneratedSourceCount);
        Assert.Contains("CreateModels()", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InterceptsLocationAttribute", result.GeneratedSource, StringComparison.Ordinal);

        var assembly = Assembly.Load(result.AssemblyImage);
        var contextType = assembly.GetType("GeneratedModels.ApplicationSqlModel", throwOnError: true)!;
        var orderType = assembly.GetType("GeneratedModels.Order", throwOnError: true)!;
        var createModels = contextType.GetMethod("CreateModels", BindingFlags.Public | BindingFlags.Static)!;
        var register = contextType.GetMethod("Register", BindingFlags.Public | BindingFlags.Static)!;
        var provider = new GeneratedEntityModelMetadataProvider();

        Assert.Null(provider.GetMetadata(orderType));
        var models = (EntityModelMetadata[])createModels.Invoke(null, null)!;
        Assert.Equal(3, models.Length);
        Assert.Null(provider.GetMetadata(orderType));

        var order = Assert.Single(models, model => model.EntityType == orderType);
        Assert.Equal("orders", order.TableName);
        Assert.Equal("sales", order.Schema);
        Assert.NotNull(order.GeneratedValueReader);

        var id = order.Properties["Id"];
        var entity = Activator.CreateInstance(orderType)!;
        id.GeneratedSetter!(entity, 73);
        Assert.Equal(73, id.GeneratedGetter!(entity));
        Assert.Equal(73, order.GeneratedValueReader!(entity, "Id"));
        Assert.Null(id.Property);

        var inherited = order.Properties["Name"];
        Assert.Equal("base_name", inherited.ColumnName);
        Assert.Equal(48, inherited.MaxLength);
        inherited.GeneratedSetter!(entity, "inherited");
        Assert.Equal("inherited", inherited.GeneratedGetter!(entity));

        var version = order.Properties["Version"];
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed,
            version.DatabaseGeneratedOption);

        Assert.Null(order.Properties["PrivateWrite"].GeneratedSetter);
        Assert.Null(order.Properties["InitOnly"].GeneratedSetter);
        Assert.Null(order.Properties["Scratch"].GeneratedSetter);
        Assert.True(order.Properties["PrivateWrite"].HasNonPublicSetter);
        Assert.False(order.Properties["InitOnly"].HasNonPublicSetter);

        var summaryType = assembly.GetType("GeneratedModels.OrderSummary", throwOnError: true)!;
        var summary = Assert.Single(models, model => model.EntityType == summaryType);
        Assert.NotNull(summary.GeneratedFactory);
        Assert.Equal(summaryType, summary.GeneratedFactory!().GetType());

        register.Invoke(null, null);
        Assert.NotNull(provider.GetMetadata(orderType));
    }

    [Fact]
    public void Generate_WhenParameterModelInheritsProperties_ShouldEmitAllGeneratedGetters()
    {
        const string source = """
            using Bing.Data.Sql.Generated;
            namespace InheritedParameterModels
            {
                public class BaseFilter { public int Value { get; set; } }
                public sealed class Filter : BaseFilter { public int Other { get; set; } }
                [SqlParameters(typeof(Filter))]
                public static partial class SqlModels { }
            }
            """;

        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        var assembly = Assembly.Load(result.AssemblyImage);
        var filterType = assembly.GetType("InheritedParameterModels.Filter", throwOnError: true)!;
        var contextType = assembly.GetType("InheritedParameterModels.SqlModels", throwOnError: true)!;
        var models = (EntityModelMetadata[])contextType.GetMethod("CreateModels")!.Invoke(null, null)!;
        var model = Assert.Single(models);
        var filter = Activator.CreateInstance(filterType)!;
        filterType.GetProperty("Value")!.SetValue(filter, 9);
        filterType.GetProperty("Other")!.SetValue(filter, 12);

        Assert.Equal(filterType, model.EntityType);
        Assert.Equal(9, model.Properties["Value"].GeneratedGetter!(filter));
        Assert.Equal(12, model.Properties["Other"].GeneratedGetter!(filter));
        Assert.Null(model.Properties["Value"].Property);
        Assert.Null(model.Properties["Other"].Property);
    }

    /// <summary>
    /// 验证不同登记角色只生成各自需要的访问器和工厂。
    /// </summary>
    [Fact]
    public void Generate_WhenModelHasSingleRole_ShouldOnlyEmitRoleCapabilities()
    {
        const string parametersSource = """
            using Bing.Data.Sql.Generated;
            namespace RoleModels
            {
                public sealed class Filter { public int Value { get; set; } }
                [SqlParameters(typeof(Filter))]
                public static partial class ParametersContext { }
            }
            """;
        const string resultSource = """
            using Bing.Data.Sql.Generated;
            namespace RoleModels
            {
                public sealed class Row { public int Value { get; set; } }
                [SqlResult(typeof(Row))]
                public static partial class ResultContext { }
            }
            """;

        var parameters = RunGenerator(parametersSource);
        AssertNoCompilationErrors(parameters);
        Assert.Contains("typeof(int), true, true", parameters.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("static instance => ((global::RoleModels.Filter)instance).Value", parameters.GeneratedSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("static (instance, value) =>", parameters.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("generatedFactory: null", parameters.GeneratedSource, StringComparison.Ordinal);

        var result = RunGenerator(resultSource);
        AssertNoCompilationErrors(result);
        Assert.Contains("typeof(int), true, true", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("static instance =>", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("static (instance, value) => ((global::RoleModels.Row)instance).Value", result.GeneratedSource,
            StringComparison.Ordinal);
        Assert.Contains("generatedFactory: static () => new global::RoleModels.Row()", result.GeneratedSource,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证共享类型的多个登记角色合并为一份完整模型元数据。
    /// </summary>
    [Fact]
    public void Generate_WhenRolesShareType_ShouldMergeCapabilitiesOnce()
    {
        const string source = """
            using Bing.Data.Sql.Generated;
            namespace RoleModels
            {
                public sealed class Shared { public int Value { get; set; } }
                [SqlParameters(typeof(Shared))]
                [SqlResult(typeof(Shared))]
                public static partial class SharedContext { }
            }
            """;

        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.Equal(1, CountOccurrences(result.GeneratedSource,
            "EntityModelMetadata(typeof(global::RoleModels.Shared)"));
        Assert.Contains("static instance => ((global::RoleModels.Shared)instance).Value", result.GeneratedSource,
            StringComparison.Ordinal);
        Assert.Contains("static (instance, value) => ((global::RoleModels.Shared)instance).Value", result.GeneratedSource,
            StringComparison.Ordinal);
        Assert.Contains("generatedFactory: static () => new global::RoleModels.Shared()", result.GeneratedSource,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证结果类型无法由生成代码物化时给出可操作诊断。
    /// </summary>
    [Fact]
    public void Generate_WhenSqlResultCannotBeMaterialized_ShouldReportActionableDiagnostic()
    {
        const string source = """
            using Bing.Data.Sql.Generated;
            public sealed class NoPublicConstructor { private NoPublicConstructor() { } }
            public abstract class AbstractResult { public AbstractResult() { } }
            public sealed class RequiredResult { public required string Name { get; set; } }
            public sealed class InitOnlyResult { public string Name { get; init; } = string.Empty; }
            [SqlResult(typeof(NoPublicConstructor))]
            [SqlResult(typeof(AbstractResult))]
            [SqlResult(typeof(RequiredResult))]
            [SqlResult(typeof(InitOnlyResult))]
            public static partial class ResultContext { }
            """;

        var result = RunGenerator(source);

        var materializationDiagnostics = result.Diagnostics
            .Where(diagnostic => diagnostic.Id == "BINGSQLM006")
            .GroupBy(diagnostic => (diagnostic.Id, diagnostic.Location.GetLineSpan().Path,
                diagnostic.Location.SourceSpan, Message: diagnostic.GetMessage()))
            .Select(group => group.First()).ToArray();
        Assert.Equal(4, materializationDiagnostics.Length);
        Assert.Contains(materializationDiagnostics, diagnostic => diagnostic.GetMessage().Contains("公共无参构造器",
            StringComparison.Ordinal));
        Assert.Contains(materializationDiagnostics, diagnostic => diagnostic.GetMessage().Contains("抽象类型",
            StringComparison.Ordinal));
        Assert.Contains(materializationDiagnostics, diagnostic => diagnostic.GetMessage().Contains("必需成员",
            StringComparison.Ordinal));
        Assert.Contains(materializationDiagnostics, diagnostic => diagnostic.GetMessage().Contains("init-only",
            StringComparison.Ordinal));
        Assert.Equal(0, result.GeneratedSourceCount);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.Id.StartsWith("CS", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证显式结果类型的非公开 setter 会报告属性诊断，而 getter-only 计算属性仍可生成。
    /// </summary>
    [Fact]
    public void Generate_WhenSqlResultHasNonPublicSetter_ShouldReportDiagnosticButAllowGetterOnlyProperty()
    {
        const string privateSetterSource = """
            using Bing.Data.Sql.Generated;
            public sealed class PrivateSetterResult { public int Value { get; private set; } }
            [SqlResult(typeof(PrivateSetterResult))]
            public partial class PrivateSetterContext { }
            """;
        const string getterOnlySource = """
            using Bing.Data.Sql.Generated;
            using System.ComponentModel.DataAnnotations.Schema;
            public sealed class ComputedResult
            {
                public int Value => 42;
                [NotMapped] public int Ignored { get; private set; }
            }
            [SqlResult(typeof(ComputedResult))]
            public partial class ComputedContext { }
            """;

        var privateSetter = RunGenerator(privateSetterSource);
        var computed = RunGenerator(getterOnlySource);

        var materializationDiagnostics = privateSetter.Diagnostics
            .Where(item => item.Id == "BINGSQLM006")
            .GroupBy(item => (item.Id, item.Location.GetLineSpan().Path,
                item.Location.SourceSpan, Message: item.GetMessage()))
            .Select(group => group.First()).ToArray();
        var diagnostic = Assert.Single(materializationDiagnostics);
        Assert.Contains("Value", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Equal(0, privateSetter.GeneratedSourceCount);
        Assert.DoesNotContain(privateSetter.Diagnostics, item => item.Severity == DiagnosticSeverity.Error &&
            item.Id.StartsWith("CS", StringComparison.Ordinal));

        AssertNoCompilationErrors(computed);
        Assert.DoesNotContain(computed.Diagnostics, item => item.Id == "BINGSQLM006");
        Assert.Equal(1, computed.GeneratedSourceCount);
        var ignoredMetadata = Assert.Single(computed.GeneratedSource.Split('\n'), line =>
            line.Contains("CreateGenerated(\"Ignored\"", StringComparison.Ordinal));
        Assert.EndsWith(", true),", ignoredMetadata.Trim(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 SqlModel 只记录非公开 setter 元数据，不将其作为 SqlResult 写入错误。
    /// </summary>
    [Fact]
    public void Generate_WhenSqlModelHasNonPublicSetter_ShouldEmitMarkerWithoutResultDiagnostic()
    {
        const string source = """
            using Bing.Data.Sql.Generated;
            public sealed class Model { public int Value { get; private set; } }
            [SqlModel(typeof(Model))]
            public partial class ModelContext { }
            """;

        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLM006");
        var propertyMetadata = Assert.Single(result.GeneratedSource.Split('\n'), line =>
            line.Contains("CreateGenerated(\"Value\"", StringComparison.Ordinal));
        Assert.EndsWith(", true),", propertyMetadata.Trim(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 SqlModel 的 public init setter 保留 CanWrite 能力，但不生成运行期写入器。
    /// </summary>
    [Fact]
    public void Generate_WhenSqlModelHasInitOnlySetter_ShouldPreserveCanWriteWithoutEmittingSetter()
    {
        const string source = """
            using Bing.Data.Sql.Generated;
            public sealed class Model { public int Value { get; init; } }
            [SqlModel(typeof(Model))]
            public partial class ModelContext { }
            """;

        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        var propertyMetadata = Assert.Single(result.GeneratedSource.Split('\n'), line =>
            line.Contains("CreateGenerated(\"Value\"", StringComparison.Ordinal));
        Assert.Contains("typeof(int), true, true,", propertyMetadata, StringComparison.Ordinal);
        Assert.EndsWith("null, false),", propertyMetadata.Trim(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证缺少 partial 的上下文会产生专用诊断且不生成冲突代码。
    /// </summary>
    [Fact]
    public void Generate_WhenContextIsNotPartial_ShouldReportDiagnostic()
    {
        const string source = """
            using Bing.Data.Sql.Generated;
            public sealed class Row { public int Id { get; set; } }
            [SqlModel(typeof(Row))]
            public class ModelContext { }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLM001");
        Assert.DoesNotContain("CreateModels()", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS0260" || diagnostic.Id == "CS0111");
    }

    /// <summary>
    /// 验证无法从生成文件访问的 file-local 模型会产生诊断。
    /// </summary>
    [Fact]
    public void Generate_WhenModelIsFileLocal_ShouldReportDiagnostic()
    {
        const string source = """
            using Bing.Data.Sql.Generated;
            file class HiddenRow { public int Id { get; set; } }
            [SqlModel(typeof(HiddenRow))]
            public partial class ModelContext { }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLM002");
        Assert.DoesNotContain("CreateModels()", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS0122");
    }

    /// <summary>
    /// 验证上下文已有生成方法名时给出专用冲突诊断。
    /// </summary>
    [Fact]
    public void Generate_WhenContextDeclaresGeneratedName_ShouldReportConflict()
    {
        const string source = """
            using Bing.Data.Sql.Generated;
            public sealed class Row { public int Id { get; set; } }
            [SqlModel(typeof(Row))]
            public partial class ModelContext
            {
                public static object[] CreateModels() => Array.Empty<object>();
            }
            """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLM003");
        Assert.DoesNotContain("CreateModels()", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "CS0111");
    }

    /// <summary>
    /// 验证生成器与缺少访问器或工厂合同的旧运行库配合时报告版本诊断。
    /// </summary>
    [Fact]
    public void Generate_WhenRuntimeContractsAreMissing_ShouldReportVersionMismatch()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations.Schema;

            namespace Bing.Data.Sql.Generated
            {
                [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
                public sealed class SqlModelAttribute : Attribute
                {
                    public SqlModelAttribute(Type modelType) { }
                }
            }

            namespace Bing.Data.Sql.Metadata
            {
                public sealed class EntityPropertyMetadata
                {
                    public static EntityPropertyMetadata CreateGenerated(string propertyName, Type clrType,
                        bool canRead, bool canWrite, string columnName = null, bool isIgnored = false,
                        bool isKey = false, DatabaseGeneratedOption databaseGeneratedOption = DatabaseGeneratedOption.None,
                        bool isConcurrencyToken = false, bool isRequired = false, int? maxLength = null,
                        string providerTypeName = null, Func<object, object> getter = null,
                        Action<object, object> setter = null) => new EntityPropertyMetadata();
                }

                public sealed class EntityModelMetadata
                {
                    public Func<object> GeneratedFactory { get; }
                    public EntityModelMetadata(Type entityType, string tableName, string schema,
                        IEnumerable<EntityPropertyMetadata> properties, Func<object> generatedFactory)
                    { GeneratedFactory = generatedFactory; }
                }

                public static class GeneratedEntityModelMetadataProvider
                {
                    public static void Register(EntityModelMetadata metadata) { }
                }
            }

            public sealed class Row { public int Id { get; set; } }
            [Bing.Data.Sql.Generated.SqlModel(typeof(Row))]
            public partial class ModelContext { }
            """;

        var result = RunGenerator(source, includeSqlRuntime: false);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLM005" &&
            diagnostic.GetMessage().Contains("升级", StringComparison.Ordinal));
        Assert.Equal(0, result.GeneratedSourceCount);
        Assert.DoesNotContain(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            diagnostic => diagnostic.Id != "BINGSQLM005");
    }

    /// <summary>
    /// 验证修改一个模型上下文只改变该上下文对应的增量输出。
    /// </summary>
    [Fact]
    public void Generate_WhenOneContextChanges_ShouldOnlyModifyItsOutput()
    {
        var first = RunIncremental(
            new SourceInput("A.cs", "using Bing.Data.Sql.Generated; public sealed class A { public int Id { get; set; } } [SqlModel(typeof(A))] public partial class ContextA { }"),
            new SourceInput("B.cs", "using Bing.Data.Sql.Generated; public sealed class B { public int Id { get; set; } } [SqlModel(typeof(B))] public partial class ContextB { }"));
        Assert.Equal(2, first.Driver.GetRunResult().Results.Single().GeneratedSources.Length);

        var changed = UpdateIncremental(first, "A.cs",
            "using Bing.Data.Sql.Generated; public sealed class A { public int Key { get; set; } } [SqlModel(typeof(A))] public partial class ContextA { }");

        var reasons = GetGenerateReasons(changed.Driver);
        Assert.Equal(2, reasons.Length);
        Assert.Single(reasons, reason => reason == IncrementalStepRunReason.Modified);
        Assert.Single(reasons, reason => reason == IncrementalStepRunReason.Cached);
        var originalGenerated = first.Driver.GetRunResult().Results.Single().GeneratedSources;
        var generated = changed.Driver.GetRunResult().Results.Single().GeneratedSources;
        Assert.Equal(2, generated.Length);
        var originalA = Assert.Single(originalGenerated, source =>
            source.SourceText.ToString().Contains("partial class ContextA", StringComparison.Ordinal));
        var changedA = Assert.Single(generated, source =>
            source.SourceText.ToString().Contains("partial class ContextA", StringComparison.Ordinal));
        Assert.NotEqual(originalA.SourceText.ToString(), changedA.SourceText.ToString());
        Assert.Contains("Key", changedA.SourceText.ToString(), StringComparison.Ordinal);

        var originalB = Assert.Single(originalGenerated, source =>
            source.SourceText.ToString().Contains("partial class ContextB", StringComparison.Ordinal));
        var changedB = Assert.Single(generated, source =>
            source.SourceText.ToString().Contains("partial class ContextB", StringComparison.Ordinal));
        Assert.Equal(originalB.SourceText.ToString(), changedB.SourceText.ToString());
    }

    /// <summary>
    /// 验证修改与 SQL 模型无关的文件不会重新生成已有上下文输出。
    /// </summary>
    [Fact]
    public void Generate_WhenUnrelatedFileChanges_ShouldSkipGenerateStep()
    {
        var first = RunIncremental(
            new SourceInput("A.cs", "using Bing.Data.Sql.Generated; public sealed class A { public int Id { get; set; } } [SqlModel(typeof(A))] public partial class ContextA { }"),
            new SourceInput("B.cs", "using Bing.Data.Sql.Generated; public sealed class B { public int Id { get; set; } } [SqlModel(typeof(B))] public partial class ContextB { }"),
            new SourceInput("Unrelated.cs", "public sealed class Unrelated { public int Value { get; set; } }"));

        var changed = UpdateIncremental(first, "Unrelated.cs",
            "public sealed class Unrelated { public string Value { get; set; } = string.Empty; }");

        var reasons = GetGenerateReasons(changed.Driver);
        Assert.Equal(2, reasons.Length);
        Assert.Equal(2, reasons.Count(reason => reason == IncrementalStepRunReason.Cached));
        Assert.DoesNotContain(reasons, reason => reason == IncrementalStepRunReason.Modified);
        Assert.Equal(2, changed.Driver.GetRunResult().Results.Single().GeneratedSources.Length);
    }

    /// <summary>
    /// 验证上下文角色变化会重新生成对应上下文。
    /// </summary>
    [Fact]
    public void Generate_WhenContextRoleChanges_ShouldRunGenerateStep()
    {
        var first = RunIncremental(new SourceInput("Context.cs",
            "using Bing.Data.Sql.Generated; public sealed class Row { public int Value { get; set; } } " +
            "[SqlParameters(typeof(Row))] public partial class ModelContext { }"));

        var changed = UpdateIncremental(first, "Context.cs",
            "using Bing.Data.Sql.Generated; public sealed class Row { public int Value { get; set; } } " +
            "[SqlModel(typeof(Row))] public partial class ModelContext { }");

        Assert.Single(GetGenerateReasons(changed.Driver), reason => reason == IncrementalStepRunReason.Modified);
        var generated = Assert.Single(changed.Driver.GetRunResult().Results.Single().GeneratedSources);
        Assert.Contains("generatedFactory: static () => new global::Row()", generated.SourceText.ToString(),
            StringComparison.Ordinal);
        Assert.Contains("static (instance, value) =>", generated.SourceText.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证运行库合同变化会使语义输入失效并报告版本诊断。
    /// </summary>
    [Fact]
    public void Generate_WhenRuntimeContractsChange_ShouldReevaluateContext()
    {
        var first = RunIncremental(false,
            new SourceInput("Runtime.cs", RuntimeContractSource(includeGeneratedContracts: true)),
            new SourceInput("Model.cs",
                "using Bing.Data.Sql.Generated; public sealed class Row { public int Value { get; set; } } " +
                "[SqlModel(typeof(Row))] public partial class ModelContext { }"));
        Assert.Single(first.Driver.GetRunResult().Results.Single().GeneratedSources);

        var changed = UpdateIncremental(first, "Runtime.cs", RuntimeContractSource(includeGeneratedContracts: false));

        Assert.Single(GetGenerateReasons(changed.Driver), reason => reason == IncrementalStepRunReason.Modified);
        Assert.Empty(changed.Driver.GetRunResult().Results.Single().GeneratedSources);
        Assert.Contains(changed.Driver.GetRunResult().Diagnostics,
            diagnostic => diagnostic.Id == "BINGSQLM005");
    }

    /// <summary>
    /// 验证运行库缺少强类型读取合同时，在生成前报告明确的版本不匹配。
    /// </summary>
    [Fact]
    public void Generate_WhenRuntimeLacksTypedReaderContract_ShouldReportVersionMismatch()
    {
        var runtime = RuntimeContractSource(includeGeneratedContracts: true).Replace(
            "public EntityPropertyMetadata WithGeneratedReader(Func<object, System.Data.Common.DbDataReader, int, bool> readerSetter) => this;", "");
        var run = RunIncremental(false, new SourceInput("Runtime.cs", runtime),
            new SourceInput("Model.cs", "using Bing.Data.Sql.Generated; public sealed class Row { public int Id { get; set; } } [SqlModel(typeof(Row))] public partial class ModelContext { }"));
        Assert.Empty(run.Driver.GetRunResult().Results.Single().GeneratedSources);
        Assert.Contains(run.Driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "BINGSQLM005");
    }

    /// <summary>
    /// 验证固定模型规模的真实 Generate 跟踪，并可选写出耗时事实 JSON。
    /// </summary>
    [Fact]
    public void Generate_WhenModelCountScales_ShouldTrackGenerateFacts()
    {
        const int warmupCount = 2;
        const int measurementCount = 5;
        const int propertiesPerModel = 4;
        var samples = new List<GeneratorProbeSample>();

        foreach (var modelCount in new[] { 1, 16, 64 })
        {
            for (var warmup = 0; warmup < warmupCount; warmup++)
                RunProbeIteration(modelCount, propertiesPerModel, warmup, false, samples);

            for (var sample = 0; sample < measurementCount; sample++)
                RunProbeIteration(modelCount, propertiesPerModel, sample, true, samples);
        }

        var outputPath = Environment.GetEnvironmentVariable("BING_SQL_GENERATOR_PROBE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var fullPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var report = new GeneratorProbeReport(warmupCount, measurementCount,
                propertiesPerModel, samples.ToArray());
            File.WriteAllText(fullPath, JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }
    }

    private static void RunProbeIteration(int modelCount, int propertiesPerModel, int iteration,
        bool measured, List<GeneratorProbeSample> samples)
    {
        var sources = CreateGeneratorProbeSources(modelCount, propertiesPerModel);
        var first = RunIncremental(sources);
        var firstSample = CaptureProbeSample(first, modelCount, propertiesPerModel,
            measured ? "first" : "warmup-first", iteration);
        if (!measured && iteration == 0)
            AssertGeneratedCompilationHasNoErrors(first);
        Assert.Equal(1, firstSample.GenerateExecutionCount);
        Assert.Equal(1, firstSample.TrackedOutputCount);
        Assert.Single(first.Driver.GetRunResult().Results.Single().GeneratedSources);
        Assert.True(firstSample.SourceBytes > 0);

        var changed = UpdateIncremental(first, "Unrelated.cs",
            "public sealed class ProbeUnrelated { public string Value { get; set; } = string.Empty; }");
        var unchangedSample = CaptureProbeSample(changed, modelCount, propertiesPerModel,
            measured ? "unrelated-file-change" : "warmup-unrelated-file-change", iteration);
        if (!measured && iteration == 0)
            AssertGeneratedCompilationHasNoErrors(changed);
        Assert.Equal(0, unchangedSample.GenerateExecutionCount);
        Assert.Equal(1, unchangedSample.TrackedOutputCount);
        Assert.Single(changed.Driver.GetRunResult().Results.Single().GeneratedSources);
        Assert.Equal(firstSample.SourceBytes, unchangedSample.SourceBytes);

        if (measured)
        {
            samples.Add(firstSample);
            samples.Add(unchangedSample);
        }
    }

    private static GeneratorProbeSample CaptureProbeSample(IncrementalRun run, int modelCount,
        int propertiesPerModel, string scenario, int iteration)
    {
        var generatorResult = run.Driver.GetRunResult().Results.Single();
        Assert.Empty(generatorResult.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var steps = generatorResult.TrackedSteps["SqlModelGenerate"];
        var outputs = steps.SelectMany(step => step.Outputs).ToArray();
        return new GeneratorProbeSample(modelCount, propertiesPerModel, scenario, iteration,
            steps.Length, outputs.Length, outputs.Count(output => output.Reason != IncrementalStepRunReason.Cached),
            steps.Sum(step => step.ElapsedTime.TotalMilliseconds),
            generatorResult.GeneratedSources.Sum(source => (long)Encoding.UTF8.GetByteCount(source.SourceText.ToString())),
            outputs.Select(output => output.Reason.ToString()).ToArray());
    }

    private static void AssertGeneratedCompilationHasNoErrors(IncrementalRun run)
    {
        var generatedSources = run.Driver.GetRunResult().Results.Single().GeneratedSources;
        var generatedTrees = generatedSources.Select(source =>
            CSharpSyntaxTree.ParseText(source.SourceText, run.ParseOptions, source.HintName));
        var generatedCompilation = run.Compilation.AddSyntaxTrees(generatedTrees);
        Assert.Empty(generatedCompilation.GetDiagnostics().Where(diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error));
    }

    private static SourceInput[] CreateGeneratorProbeSources(int modelCount, int propertiesPerModel)
    {
        var models = new StringBuilder("using Bing.Data.Sql.Generated;\nnamespace GeneratorProbe {\n");
        for (var modelIndex = 0; modelIndex < modelCount; modelIndex++)
        {
            var modelName = "Model" + modelIndex.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
            models.Append("public sealed class ").Append(modelName).AppendLine(" {");
            for (var propertyIndex = 0; propertyIndex < propertiesPerModel; propertyIndex++)
                models.Append("public int Value").Append(propertyIndex).AppendLine(" { get; set; }");
            models.AppendLine("}");
        }

        foreach (var modelIndex in Enumerable.Range(0, modelCount))
        {
            var modelName = "Model" + modelIndex.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
            models.Append("[SqlModel(typeof(").Append(modelName).AppendLine("))]");
        }
        models.AppendLine("public static partial class ProbeContext { }");
        models.AppendLine("}");
        return new[]
        {
            new SourceInput("ProbeModels.cs", models.ToString()),
            new SourceInput("Unrelated.cs", "public sealed class ProbeUnrelated { public int Value { get; set; } }")
        };
    }

    private static GeneratorResult RunGenerator(string source, bool includeSqlRuntime = true)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), parseOptions, "ModelConsumer.cs");
        var compilation = CSharpCompilation.Create(
            "SqlModelConsumer_" + Guid.NewGuid().ToString("N"),
            new[] { syntaxTree },
            CreateMetadataReferences(includeSqlRuntime),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Disable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new SqlModelGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var driverDiagnostics);

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.Results.SelectMany(result => result.GeneratedSources).ToArray();
        var generatedSource = string.Join(Environment.NewLine, generatedSources.Select(result => result.SourceText.ToString()));
        using var assemblyStream = new MemoryStream();
        var emitResult = updatedCompilation.Emit(assemblyStream);
        var diagnostics = driverDiagnostics
            .Concat(runResult.Diagnostics)
            .Concat(updatedCompilation.GetDiagnostics())
            .Concat(emitResult.Diagnostics)
            .ToImmutableArray();
        return new GeneratorResult(generatedSource, generatedSources.Length, diagnostics, emitResult.Success,
            assemblyStream.ToArray());
    }

    private static IncrementalRun RunIncremental(params SourceInput[] sources) =>
        RunIncremental(true, sources);

    private static IncrementalRun RunIncremental(bool includeSqlRuntime, params SourceInput[] sources)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(
            SourceText.From(source.Source, Encoding.UTF8), parseOptions, source.Path)).ToArray();
        var compilation = CSharpCompilation.Create(
            "SqlModelIncremental_" + Guid.NewGuid().ToString("N"), trees,
            CreateMetadataReferences(includeSqlRuntime),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Disable));
        var driverOptions = new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new SqlModelGenerator().AsSourceGenerator() },
            parseOptions: parseOptions,
            driverOptions: driverOptions);
        driver = driver.RunGenerators(compilation);
        return new IncrementalRun(driver, compilation, parseOptions);
    }

    private static IncrementalRun UpdateIncremental(IncrementalRun previous, string path, string source)
    {
        var oldTree = previous.Compilation.SyntaxTrees.Single(tree => tree.FilePath == path);
        var newTree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), previous.ParseOptions, path);
        var compilation = previous.Compilation.ReplaceSyntaxTree(oldTree, newTree);
        var driver = previous.Driver.RunGenerators(compilation);
        return new IncrementalRun(driver, compilation, previous.ParseOptions);
    }

    private static ImmutableArray<IncrementalStepRunReason> GetGenerateReasons(GeneratorDriver driver) =>
        driver.GetRunResult().Results.Single().TrackedSteps["SqlModelGenerate"]
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ToImmutableArray();

    private static string RuntimeContractSource(bool includeGeneratedContracts)
    {
        var propertyFactory = includeGeneratedContracts
            ? "public static EntityPropertyMetadata CreateGenerated(string propertyName, Type clrType, " +
              "bool canRead, bool canWrite, string columnName, bool isIgnored, bool isKey, " +
              "DatabaseGeneratedOption databaseGeneratedOption, bool isConcurrencyToken, bool isRequired, " +
              "int? maxLength, string providerTypeName, Func<object, object> getter, " +
              "Action<object, object> setter, bool hasNonPublicSetter) => new EntityPropertyMetadata(); " +
              "public EntityPropertyMetadata WithGeneratedReader(Func<object, System.Data.Common.DbDataReader, int, bool> readerSetter) => this;"
            : "public static EntityPropertyMetadata CreateLegacy() => new EntityPropertyMetadata();";
        var modelFactory = includeGeneratedContracts
            ? "public Func<object> GeneratedFactory { get; } " +
              "public EntityModelMetadata(Type entityType, string tableName, string schema, " +
              "IEnumerable<EntityPropertyMetadata> properties, Func<object> generatedFactory) { " +
              "GeneratedFactory = generatedFactory; }"
            : "public EntityModelMetadata(Type entityType, string tableName, string schema, " +
              "IEnumerable<EntityPropertyMetadata> properties) { }";
        return """
            using System;
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations.Schema;
            namespace Bing.Data.Sql.Generated
            {
                [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
                public sealed class SqlModelAttribute : Attribute
                {
                    public SqlModelAttribute(Type modelType) { }
                }
            }
            namespace Bing.Data.Sql.Metadata
            {
                public sealed class EntityPropertyMetadata
                {
                    __PROPERTY_FACTORY__
                }
                public sealed class EntityModelMetadata
                {
                    __MODEL_FACTORY__
                }
                public static class GeneratedEntityModelMetadataProvider
                {
                    public static void Register(EntityModelMetadata metadata) { }
                }
            }
            """.Replace("__PROPERTY_FACTORY__", propertyFactory, StringComparison.Ordinal)
            .Replace("__MODEL_FACTORY__", modelFactory, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string fragment)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }
        return count;
    }

    private static ImmutableArray<MetadataReference> CreateMetadataReferences(bool includeSqlRuntime = true)
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var paths = trustedAssemblies.Split(Path.PathSeparator).ToList();
        if (includeSqlRuntime)
            paths.Add(typeof(SqlModelAttribute).Assembly.Location);
        else
            paths.RemoveAll(path => string.Equals(Path.GetFileName(path), "Bing.Data.Sql.dll",
                StringComparison.OrdinalIgnoreCase));
        return paths.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToImmutableArray<MetadataReference>();
    }

    private static void AssertNoCompilationErrors(GeneratorResult result)
    {
        var errors = result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(result.EmitSucceeded, string.Join(Environment.NewLine,
            result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.Empty(errors);
    }

    private sealed record SourceInput(string Path, string Source);

    private sealed record IncrementalRun(GeneratorDriver Driver, Compilation Compilation, CSharpParseOptions ParseOptions);

    private sealed record GeneratorProbeReport(int WarmupCount, int MeasurementCount,
        int PropertiesPerModel, GeneratorProbeSample[] Samples);

    private sealed record GeneratorProbeSample(int ModelCount, int PropertiesPerModel, string Scenario,
        int Iteration, int TrackedStepCount, int TrackedOutputCount, int GenerateExecutionCount,
        double ElapsedMilliseconds, long SourceBytes, string[] Reasons);

    private sealed record GeneratorResult(string GeneratedSource, int GeneratedSourceCount,
        ImmutableArray<Diagnostic> Diagnostics, bool EmitSucceeded, byte[] AssemblyImage);
}
