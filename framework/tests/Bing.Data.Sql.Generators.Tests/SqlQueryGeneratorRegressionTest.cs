using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using System.Runtime.CompilerServices;
using Bing.Data.Sql.Metadata;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>
    /// 实际加载生成程序集后逐字段对照反射元数据，覆盖继承、override、忽略及写入约束。
    /// </summary>
    [Fact]
    public void Generate_WhenAnnotationsAreInherited_ShouldMatchReflectionMetadata()
    {
        const string source = """
            using System;
            using System.ComponentModel.DataAnnotations;
            using System.ComponentModel.DataAnnotations.Schema;
            using Bing.Data.Sql;
            [Table("InheritedRows", Schema = "archive")]
            public class BaseRow
            {
                [Key, Column("RowId")] public int Id { get; set; }
                [Column("BaseName", TypeName = "TEXT"), Required, MaxLength(-1), StringLength(12)]
                public virtual string Name { get; set; }
                [NotMapped] public virtual string Hidden { get; set; }
                [Timestamp, DatabaseGenerated(DatabaseGeneratedOption.None)] public byte[] Version { get; set; }
                [DatabaseGenerated(DatabaseGeneratedOption.Identity)] public long Sequence { get; set; }
            }
            public class DerivedRow : BaseRow
            {
                public override string Name { get; set; }
                public override string Hidden { get; set; }
                [Fake.Column("wrong")] public string Ordinary { get; set; }
            }
            [Table("ReplacedRows")]
            public class ReplacedRow : BaseRow
            {
                [Column("ReplacedName"), StringLength(30)] public override string Name { get; set; }
                public new string Hidden { get; set; }
            }
            namespace Fake { public class ColumnAttribute : Attribute { public ColumnAttribute(string value) {} } }
            public static class Consumer
            {
                public static void Register(SqlLambdaQuery q) { q.From<DerivedRow>(); q.From<ReplacedRow>(); }
            }
            """;
        var result = RunGenerator(source);
        var assembly = LoadGeneratedAssembly(result);
        foreach (var name in new[] { "DerivedRow", "ReplacedRow" })
        {
            var type = assembly.GetType(name)!;
            var expected = new DataAnnotationsEntityModelMetadataProvider().GetMetadata(type);
            var actual = new GeneratedEntityModelMetadataProvider().GetMetadata(type);
            Assert.NotNull(actual);
            Assert.Equal(expected.TableName, actual.TableName);
            Assert.Equal(expected.Schema, actual.Schema);
            Assert.Equal(expected.Properties.Keys.OrderBy(key => key), actual.Properties.Keys.OrderBy(key => key));
            foreach (var entry in expected.Properties)
            {
                var left = entry.Value;
                var right = actual.Properties[entry.Key];
                Assert.Equal(
                    (left.ColumnName, left.ClrType, left.CanRead, left.CanWrite, left.IsIgnored, left.IsKey,
                        left.DatabaseGeneratedOption, left.IsConcurrencyToken, left.IsRequired, left.MaxLength,
                        left.ProviderTypeName, left.IsNullable, left.CanInsert, left.CanUpdate),
                    (right.ColumnName, right.ClrType, right.CanRead, right.CanWrite, right.IsIgnored, right.IsKey,
                        right.DatabaseGeneratedOption, right.IsConcurrencyToken, right.IsRequired, right.MaxLength,
                        right.ProviderTypeName, right.IsNullable, right.CanInsert, right.CanUpdate));
                Assert.Null(right.Property);
            }
            Assert.Equal(DatabaseGeneratedOption.Computed, actual.Properties["Version"].DatabaseGeneratedOption);
            Assert.False(actual.Properties["Version"].CanInsert);
            Assert.False(actual.Properties["Version"].CanUpdate);
        }
    }

    /// <summary>
    /// 执行生成读取器，二进制值、DBNull 和排除列均应保留对应语义。
    /// </summary>
    [Fact]
    public void Generate_WhenBinaryColumnIsRead_ShouldPreserveBytesNullAndExcludedDefault()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;
            public class BinaryRow { public int Id { get; set; } public byte[] Payload { get; set; } }
            public static class Consumer
            {
                public static List<BinaryRow> Load(SqlLambdaQuery query) => query.ToList<BinaryRow>();
            }
            """;
        var assembly = LoadGeneratedAssembly(RunGenerator(source));
        var calls = assembly.GetType("Bing.Data.Sql.Generated.QueryCalls")!;
        var readerMethod = calls.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Single(method => method.Name.StartsWith("Read_", StringComparison.Ordinal));
        using var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Payload", typeof(byte[]));
        table.Rows.Add(1, new byte[] { 1, 2, 3 });
        table.Rows.Add(2, DBNull.Value);
        using var reader = table.CreateDataReader();
        object Read(bool includePayload)
        {
            var ordinals = new Dictionary<string, int> { ["Id"] = 0 };
            if (includePayload) ordinals.Add("Payload", 1);
            var materialize = (Delegate)readerMethod.Invoke(null, new object[] { ordinals })!;
            return materialize.DynamicInvoke(reader)!;
        }
        Assert.True(reader.Read());
        var first = Read(true);
        var property = first.GetType().GetProperty("Payload")!;
        Assert.Equal(new byte[] { 1, 2, 3 }, (byte[])property.GetValue(first)!);
        Assert.Null(property.GetValue(Read(false)));
        Assert.True(reader.Read());
        Assert.Null(property.GetValue(Read(true)));
    }

    /// <summary>
    /// 默认实体投影使用物理列名时，生成读取器仍应填充 CLR 属性。
    /// </summary>
    [Fact]
    public void Generate_WhenColumnIsMapped_ShouldReadPhysicalOrdinal()
    {
        const string source = """
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations.Schema;
            using Bing.Data.Sql;
            public class Row
            {
                [Column("physical_id")] public int Id { get; set; }
            }
            public static class Consumer
            {
                public static List<Row> Load(SqlLambdaQuery query) => query.ToList<Row>();
            }
            """;
        var assembly = LoadGeneratedAssembly(RunGenerator(source));
        var calls = assembly.GetType("Bing.Data.Sql.Generated.QueryCalls")!;
        var readerMethod = calls.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name.StartsWith("Read_", StringComparison.Ordinal));
        using var table = new DataTable();
        table.Columns.Add("physical_id", typeof(int));
        table.Rows.Add(9);
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var ordinals = new Dictionary<string, int> { ["physical_id"] = 0 };
        var materialize = (Delegate)readerMethod.Invoke(null, new object[] { ordinals })!;
        var result = materialize.DynamicInvoke(reader)!;
        Assert.Equal(9, (int)result.GetType().GetProperty("Id")!.GetValue(result)!);
    }

    /// <summary>
    /// SQLite 风格的 Int64 整数列应能物化到 Int32 属性。
    /// </summary>
    [Fact]
    public void Generate_WhenIntegerColumnUsesInt64Storage_ShouldConvertIntegerProperties()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;
            public enum Status { None, Active }
            public class Row
            {
                public int Id { get; set; }
                public short Rank { get; set; }
                public byte Level { get; set; }
                public Status Status { get; set; }
            }
            public static class Consumer
            {
                public static List<Row> Load(SqlLambdaQuery query) => query.ToList<Row>();
            }
            """;
        var assembly = LoadGeneratedAssembly(RunGenerator(source));
        var calls = assembly.GetType("Bing.Data.Sql.Generated.QueryCalls")!;
        var readerMethod = calls.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name.StartsWith("Read_", StringComparison.Ordinal));
        using var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("Rank", typeof(long));
        table.Columns.Add("Level", typeof(long));
        table.Columns.Add("Status", typeof(long));
        table.Rows.Add(9L, 3L, 2L, 1L);
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var ordinals = new Dictionary<string, int> { ["Id"] = 0, ["Rank"] = 1, ["Level"] = 2, ["Status"] = 3 };
        var materialize = (Delegate)readerMethod.Invoke(null, new object[] { ordinals })!;
        var result = materialize.DynamicInvoke(reader)!;
        Assert.Equal(9, (int)result.GetType().GetProperty("Id")!.GetValue(result)!);
        Assert.Equal((short)3, (short)result.GetType().GetProperty("Rank")!.GetValue(result)!);
        Assert.Equal((byte)2, (byte)result.GetType().GetProperty("Level")!.GetValue(result)!);
        Assert.Equal("Active", result.GetType().GetProperty("Status")!.GetValue(result)!.ToString());
    }

    /// <summary>
    /// 命名标量投影应读取投影列的实际值，并保留可空列的 DBNull 语义。
    /// </summary>
    /// <param name="resultTypeName">查询结果的标量类型名称。</param>
    /// <param name="databaseValue">数据读取器中的列值。</param>
    /// <param name="expectedValue">生成读取器应返回的值。</param>
    /// <param name="reverseOrdinalEntries">是否按 1、0 的顺序插入列序号映射。</param>
    [Theory]
    [MemberData(nameof(ScalarResultReadCases))]
    public void Generate_WhenNamedScalarResultIsRead_ShouldMaterializeProjectedValue(
        string resultTypeName, object databaseValue, object expectedValue, bool reverseOrdinalEntries)
    {
        var source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;
            public class Row { public long Id { get; set; } }
            public static class Consumer
            {
                public static List<RESULTTYPE> Load(SqlLambdaQuery query)
                {
                    var description = query.From<Row>("o").Select<Row>(o => o.Id).OrderBy<Row>(o => o.Id);
                    return description.ToList<RESULTTYPE>();
                }
            }
            """.Replace("RESULTTYPE", resultTypeName, StringComparison.Ordinal);
        var generated = RunGenerator(source);
        var assembly = LoadGeneratedAssembly(generated);
        var resultType = resultTypeName switch
        {
            "long" => typeof(long),
            "long?" => typeof(long?),
            "int" => typeof(int),
            _ => throw new ArgumentOutOfRangeException(nameof(resultTypeName))
        };
        var readerMethod = assembly.GetType("Bing.Data.Sql.Generated.QueryCalls")!
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => method.Name.StartsWith("Read_", StringComparison.Ordinal) &&
                method.ReturnType.IsGenericType &&
                method.ReturnType.GetGenericTypeDefinition() == typeof(Func<,>) &&
                method.ReturnType.GetGenericArguments()[1] == resultType);
        Assert.NotNull(readerMethod);

        using var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("Other", typeof(long));
        table.Rows.Add(databaseValue, 99L);
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var ordinals = reverseOrdinalEntries
            ? new Dictionary<string, int> { ["Other"] = 1, ["Id"] = 0 }
            : new Dictionary<string, int> { ["Id"] = 0 };
        var materialize = (Delegate)readerMethod.Invoke(null, new object[] { ordinals })!;

        Assert.Equal(expectedValue, materialize.DynamicInvoke(reader));
    }

    /// <summary>
    /// 列举 long、long?、int 和乱序列序号的标量读取场景。
    /// </summary>
    public static IEnumerable<object[]> ScalarResultReadCases()
    {
        yield return new object[] { "long", 17L, 17L, false };
        yield return new object[] { "long?", 17L, 17L, false };
        yield return new object[] { "long?", DBNull.Value, null!, false };
        yield return new object[] { "int", 17L, 17, false };
        yield return new object[] { "long", 17L, 17L, true };
    }

    /// <summary>
    /// 已声明支持的无符号、窄整数及字符列应生成可执行的静态读取器。
    /// </summary>
    [Fact]
    public void Generate_WhenUnsignedAndCharacterColumnsAreRead_ShouldMaterializeWithoutReflectionFallback()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Data.Sql;
            public enum UnsignedStatus : uint { None = 0, Ready = 7 }
            public class Row
            {
                public sbyte Small { get; set; }
                public ushort Medium { get; set; }
                public uint Count { get; set; }
                public ulong Total { get; set; }
                public char Marker { get; set; }
                public UnsignedStatus Status { get; set; }
            }
            public static class Consumer
            {
                public static List<Row> Load(SqlLambdaQuery query) => query.ToList<Row>();
            }
            """;
        var assembly = LoadGeneratedAssembly(RunGenerator(source));
        var calls = assembly.GetType("Bing.Data.Sql.Generated.QueryCalls")!;
        var readerMethod = calls.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name.StartsWith("Read_", StringComparison.Ordinal));
        using var table = new DataTable();
        table.Columns.Add("Small", typeof(sbyte));
        table.Columns.Add("Medium", typeof(ushort));
        table.Columns.Add("Count", typeof(uint));
        table.Columns.Add("Total", typeof(ulong));
        table.Columns.Add("Marker", typeof(char));
        table.Columns.Add("Status", typeof(uint));
        table.Rows.Add((sbyte)-3, (ushort)500, 42u, 9000ul, 'Z', 7u);
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var ordinals = new Dictionary<string, int>
        {
            ["Small"] = 0, ["Medium"] = 1, ["Count"] = 2, ["Total"] = 3,
            ["Marker"] = 4, ["Status"] = 5
        };
        var materialize = (Delegate)readerMethod.Invoke(null, new object[] { ordinals })!;
        var result = materialize.DynamicInvoke(reader)!;
        Assert.Equal((sbyte)-3, (sbyte)result.GetType().GetProperty("Small")!.GetValue(result)!);
        Assert.Equal((ushort)500, (ushort)result.GetType().GetProperty("Medium")!.GetValue(result)!);
        Assert.Equal(42u, (uint)result.GetType().GetProperty("Count")!.GetValue(result)!);
        Assert.Equal(9000ul, (ulong)result.GetType().GetProperty("Total")!.GetValue(result)!);
        Assert.Equal('Z', (char)result.GetType().GetProperty("Marker")!.GetValue(result)!);
        Assert.Equal("Ready", result.GetType().GetProperty("Status")!.GetValue(result)!.ToString());
    }

    /// <summary>
    /// 显式映射的未支持值对象和标量类型不能静默按导航跳过。
    /// </summary>
    /// <param name="type">用于生成测试代码的类型名称。</param>
    /// <param name="annotation">待生成代码中的类型注解。</param>
    [Theory]
    [InlineData("System.DateTimeOffset", "")]
    [InlineData("System.Version", "[System.ComponentModel.DataAnnotations.Schema.Column(\"value\")]")]
    public void Generate_WhenMappedScalarCannotBeRead_ShouldReportStrictDiagnostic(string type, string annotation)
    {
        var source = "using Bing.Data.Sql; public class Row { public int Id { get; set; } " + annotation +
            " public " + type + " Value { get; set; } } public class C { public object Read(SqlLambdaQuery q) => q.ToList<Row>(); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001" && diagnostic.Location.IsInSource);
    }

    /// <summary>
    /// 连接选项作为原实参传给生成桥接，不在生成阶段丢失或重复求值。
    /// </summary>
    /// <param name="operation">用于选择被测操作的名称。</param>
    [Theory]
    [InlineData("Join")]
    [InlineData("LeftJoin")]
    public void Generate_WhenJoinUsesOptions_ShouldForwardEvaluatedOptions(string operation)
    {
        var source = """
            using Bing.Data.Sql;
            public class Row { public int Id { get; set; } public int ParentId { get; set; } }
            public class C
            {
                public SqlLambdaQuery Build(SqlLambdaQuery q, SqlJoinOptions options) =>
                    q.OP<Row, Row>((b,c) => b.ParentId == c.Id, options);
            }
            """.Replace("OP<", operation + "<", StringComparison.Ordinal);
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.Contains("\"ParentId\", \"Id\", @options)", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// 加载生成的程序集并执行模块初始化器。
    /// </summary>
    /// <param name="result">生成器运行及编译结果。</param>
    /// <returns>已执行生成模块初始化器的程序集。</returns>
    private static Assembly LoadGeneratedAssembly(GeneratorResult result)
    {
        AssertNoCompilationErrors(result);
        var assembly = Assembly.Load(result.AssemblyImage);
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        return assembly;
    }
}
