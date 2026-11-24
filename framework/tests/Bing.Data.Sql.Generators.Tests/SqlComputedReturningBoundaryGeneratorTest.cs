using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    private const string ComputedBoundaryConsumer = """
        using Bing.Data.Sql;
        using Bing.Data.Sql.Builders.Mutations;

        public sealed class ComputedBoundaryRow
        {
            public int Id { get; set; }
            public long LongValue { get; set; }
            public decimal DecimalValue { get; set; }
            public int? OptionalCount { get; set; }
            public string Name { get; set; }
        }

        public static class ComputedBoundaryConsumer
        {
            public static ISqlUpdateBuilder Apply(ISqlUpdateBuilder source) =>
                source.Returning<ComputedBoundaryRow>(row => new
                {
                    DecimalTotal = (((row.DecimalValue + row.DecimalValue) - row.DecimalValue) * row.DecimalValue) / row.DecimalValue,
                    OptionalTotal = (((row.OptionalCount + row.OptionalCount) - row.OptionalCount) * row.OptionalCount) / row.OptionalCount
                });

            public static ISqlUpdateBuilder ApplyAlias(ISqlUpdateBuilder source) =>
                source.Returning<ComputedBoundaryRow>(row => new { Total = (row.Id + row.Id) * row.Id, row.Name });

            public static ISqlUpdateBuilder ApplyConvertedMixed(ISqlUpdateBuilder source) =>
                source.Returning<ComputedBoundaryRow>(row => new
                {
                    Converted = (long)row.Id,
                    Total = row.Id + row.Id
                });

            public static ISqlUpdateBuilder ApplyAppend(ISqlUpdateBuilder source) =>
                source.Returning<ComputedBoundaryRow>(row => new { Total = (row.Id + row.Id) * row.Id })
                    .Returning<ComputedBoundaryRow>(row => new { row.Name });
        }
        """;

    /// <summary>decimal 与可空 int 的四则运算应生成并执行三个 Provider 的完整 SQL。</summary>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("sqlserver")]
    public void GeneratedReturning_WhenDecimalAndNullableArithmeticIsExecuted_ShouldRenderCompleteSql(string providerKey)
    {
        var result = RunGenerator(ComputedBoundaryConsumer);

        AssertNoCompilationErrors(result);
        Assert.Contains("ApplyComputedReturning<", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlReturningColumn.CreateComputed(new string[]", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");

        var provider = GetReturningProvider(providerKey);
        var builder = CreateGeneratedSqlReturningExecutionBuilder(provider, new ComputedBoundaryMappingResolver());
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "Apply", builder, "ComputedBoundaryConsumer");

        var expectedSql = providerKey switch
        {
            "sqlserver" => "Update [samples] Set [Name] = @_p_0 Output " +
                "(((([Inserted].[DecimalValue] + [Inserted].[DecimalValue]) - [Inserted].[DecimalValue]) * " +
                "[Inserted].[DecimalValue]) / [Inserted].[DecimalValue]) As [DecimalTotal], " +
                "(((([Inserted].[OptionalCount] + [Inserted].[OptionalCount]) - [Inserted].[OptionalCount]) * " +
                "[Inserted].[OptionalCount]) / [Inserted].[OptionalCount]) As [OptionalTotal] " +
                "Where [Id]=@_p_1",
            "postgresql" => "Update \"samples\" Set \"Name\" = @_p_0 Where \"Id\"=@_p_1 Returning " +
                "((((\"DecimalValue\" + \"DecimalValue\") - \"DecimalValue\") * \"DecimalValue\") / \"DecimalValue\") As \"DecimalTotal\", " +
                "((((\"OptionalCount\" + \"OptionalCount\") - \"OptionalCount\") * \"OptionalCount\") / \"OptionalCount\") As \"OptionalTotal\"",
            _ => "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning " +
                "((((`DecimalValue` + `DecimalValue`) - `DecimalValue`) * `DecimalValue`) / `DecimalValue`) As `DecimalTotal`, " +
                "((((`OptionalCount` + `OptionalCount`) - `OptionalCount`) * `OptionalCount`) / `OptionalCount`) As `OptionalTotal`"
        };

        Assert.Equal(expectedSql, builder.ToSql());
        Assert.Equal(expectedSql, builder.ToSql());
        Assert.Equal(2, builder.MutationContext.ParameterManager.Count);
    }

    /// <summary>不支持的表达式必须产生回退诊断，运行时拒绝时保留已有投影。</summary>
    [Theory]
    [InlineData("checked(row.Id + row.Id)", "checked")]
    [InlineData("row.Id + captured", "captured")]
    [InlineData("row.Id + 1", "constant")]
    [InlineData("row.Name + row.Name", "string")]
    [InlineData("row.Id + (long)row.LongValue", "mixed")]
    [InlineData("(long)(row.Id + row.Id)", "cast")]
    public void Generate_WhenComputedReturningShapeIsUnsupported_ShouldKeepRuntimePath(string expression, string runtimeCase)
    {
        var source = CreateUnsupportedComputedConsumer(expression);
        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        Assert.DoesNotContain("ApplyComputedReturning<", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateComputed(", result.GeneratedSource, StringComparison.Ordinal);

        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance,
            new ComputedBoundaryMappingResolver());
        builder.Returning<RuntimeComputedBoundaryRow>(row => new { row.Name });
        var before = builder.ToSql();
        var projection = CreateUnsupportedRuntimeProjection(runtimeCase);

        Assert.Throws<ArgumentException>(() => builder.Returning<RuntimeComputedBoundaryRow>(projection));
        Assert.Equal(before, builder.ToSql());
        Assert.Equal(2, builder.MutationContext.ParameterManager.Count);
    }

    /// <summary>表别名和定制方言应回退到运行时格式化计算列。</summary>
    [Fact]
    public void GeneratedReturning_WhenQualifierOrCustomDialectIsPresent_ShouldUseRuntimeFormatting()
    {
        var result = RunGenerator(ComputedBoundaryConsumer);
        AssertNoCompilationErrors(result);
        var assembly = LoadGeneratedAssembly(result);
        var resolver = new ComputedBoundaryMappingResolver();
        var aliased = new SqlUpdateBuilder(PostgreSqlSqlProvider.Instance,
                new SqlBuilderServices(entityMappingResolver: resolver))
            .Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .Set("Name", "updated")
            .Where("Id", 1);
        ApplyGeneratedReturning(assembly, "ApplyAlias", aliased, "ComputedBoundaryConsumer");

        Assert.Equal("Update \"samples\" As \"t\" Set \"Name\" = @_p_0 Where \"Id\"=@_p_1 " +
            "Returning ((\"t\".\"Id\" + \"t\".\"Id\") * \"t\".\"Id\") As \"Total\", " +
            "\"t\".\"Name\" As \"Name\"", aliased.ToSql());

        var customProvider = new ComputedBoundaryDialectProvider(SqliteSqlProvider.Instance);
        var customized = CreateGeneratedSqlReturningExecutionBuilder(customProvider, resolver);
        ApplyGeneratedReturning(assembly, "ApplyAlias", customized, "ComputedBoundaryConsumer");

        Assert.Equal("Update <samples> Set <Name> = @_p_0 Where <Id>=@_p_1 " +
            "Returning ((<Id> + <Id>) * <Id>) As <Total>, <Name> As <Name>", customized.ToSql());
    }

    /// <summary>直接列的单层数值转换与计算列混合时仍作为映射布局处理。</summary>
    [Fact]
    public void GeneratedReturning_WhenDirectConvertedColumnIsMixedWithArithmetic_ShouldPreserveLayout()
    {
        var result = RunGenerator(ComputedBoundaryConsumer);

        AssertNoCompilationErrors(result);
        Assert.Contains("ApplyComputedReturning<", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");

        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance,
            new ComputedBoundaryMappingResolver());
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyConvertedMixed", builder, "ComputedBoundaryConsumer");

        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
            "Returning `Id` As `Converted`, (`Id` + `Id`) As `Total`", builder.ToSql());
    }

    /// <summary>计算型静态路径追加普通列后应能克隆并独立清空。</summary>
    [Fact]
    public void GeneratedReturning_WhenProjectionIsAppendedAndBuilderCloned_ShouldKeepIndependentState()
    {
        var result = RunGenerator(ComputedBoundaryConsumer);
        AssertNoCompilationErrors(result);
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance,
            new ComputedBoundaryMappingResolver());
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyAppend", builder, "ComputedBoundaryConsumer");
        const string expected = "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
            "Returning ((`Id` + `Id`) * `Id`) As `Total`, `Name` As `Name`";

        Assert.Equal(expected, builder.ToSql());
        var clone = builder.Clone();
        Assert.Equal(expected, clone.ToSql());

        builder.Clear();

        Assert.True(builder.ReturningClause.IsEmpty);
        Assert.Equal(expected, clone.ToSql());
    }

    /// <summary>旧运行库缺少或签名不同的 CreateComputed 合同不能产生不可编译引用。</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("instance")]
    public void Generate_WhenComputedReturningContractIsMissingOrIncompatible_ShouldKeepRuntimeCalls(string variant)
    {
        var result = RunGenerator(ComputedBoundaryConsumer,
            metadataReferences: ComputedReturningRuntimeReferences(variant));

        AssertNoCompilationErrors(result);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        Assert.DoesNotContain("ApplyComputedReturning<", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateComputed(", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>参数 Optional 标记和名称不影响位置参数形式的生成桥接。</summary>
    [Theory]
    [InlineData("qualifier-required")]
    [InlineData("alias-renamed")]
    public void Generate_WhenComputedReturningParametersArePositionallyCompatible_ShouldCompileAndRender(string variant)
    {
        var result = RunGenerator(ComputedBoundaryConsumer,
            metadataReferences: ComputedReturningRuntimeReferences(variant));

        AssertNoCompilationErrors(result);
        Assert.Contains("ApplyComputedReturning<", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");

        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance,
            new ComputedBoundaryMappingResolver());
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyAlias", builder, "ComputedBoundaryConsumer");

        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
            "Returning ((`Id` + `Id`) * `Id`) As `Total`, `Name` As `Name`", builder.ToSql());
    }

    // 仅在内存中修改待编译引用的可选方法；保持运行库内其余类型身份一致，避免同名源码遮蔽造成假失败。
    // 变体用于元数据发现及消费编译，不执行修改后的程序集，也不写入磁盘。
    private static ImmutableArray<MetadataReference> ComputedReturningRuntimeReferences(string variant,
        string methodName = "CreateComputed")
    {
        var runtimePath = typeof(SqlReturningColumn).Assembly.Location;
        var image = File.ReadAllBytes(runtimePath);
        using (var pe = new PEReader(new MemoryStream(image)))
        {
            var reader = pe.GetMetadataReader();
            var type = reader.TypeDefinitions.Select(reader.GetTypeDefinition).Single(item =>
                reader.GetString(item.Namespace) == "Bing.Data.Sql.Builders.Mutations" &&
                reader.GetString(item.Name) == "SqlReturningColumn");
            var methodHandle = type.GetMethods().Single(handle =>
                reader.GetString(reader.GetMethodDefinition(handle).Name) == methodName);
            var metadataStart = pe.PEHeaders.MetadataStartOffset;
            if (variant == "instance")
            {
                // MethodDef 行：RVA(4)、ImplFlags(2)、Flags(2)；移除 Static 以模拟不兼容合同。
                var flagsOffset = metadataStart + reader.GetTableMetadataOffset(TableIndex.MethodDef) +
                    (MetadataTokens.GetRowNumber(methodHandle) - 1) * reader.GetTableRowSize(TableIndex.MethodDef) + 6;
                image[flagsOffset] = (byte)(image[flagsOffset] & ~(byte)MethodAttributes.Static);
            }
            else if (variant == "missing")
            {
                var name = reader.GetMethodDefinition(methodHandle).Name;
                var nameOffset = metadataStart + reader.GetHeapMetadataOffset(HeapIndex.String) + MetadataTokens.GetHeapOffset(name);
                image[nameOffset] = (byte)'X';
            }
            else if (variant == "qualifier-required")
            {
                var qualifier = reader.GetMethodDefinition(methodHandle).GetParameters()
                    .Single(handle => reader.GetParameter(handle).SequenceNumber == 2);
                var flagsOffset = metadataStart + reader.GetTableMetadataOffset(TableIndex.Param) +
                    (MetadataTokens.GetRowNumber(qualifier) - 1) *
                    reader.GetTableRowSize(TableIndex.Param);
                var flags = (ushort)(image[flagsOffset] | image[flagsOffset + 1] << 8);
                flags = (ushort)(flags & ~(ushort)ParameterAttributes.Optional);
                image[flagsOffset] = (byte)flags;
                image[flagsOffset + 1] = (byte)(flags >> 8);
            }
            else if (variant == "alias-renamed")
            {
                var alias = reader.GetMethodDefinition(methodHandle).GetParameters().Select(reader.GetParameter)
                    .Single(parameter => parameter.SequenceNumber == 3);
                var nameOffset = metadataStart + reader.GetHeapMetadataOffset(HeapIndex.String) +
                    MetadataTokens.GetHeapOffset(alias.Name);
                Array.Copy(Encoding.ASCII.GetBytes("label"), 0, image, nameOffset, 5);
            }
            else throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
        }
        return CreateMetadataReferences().Select(reference => reference is PortableExecutableReference file &&
            string.Equals(file.FilePath, runtimePath, StringComparison.OrdinalIgnoreCase)
                ? MetadataReference.CreateFromImage(image).WithProperties(reference.Properties)
                : reference).ToImmutableArray();
    }

    private static string CreateUnsupportedComputedConsumer(string expression) =>
        "using Bing.Data.Sql; using Bing.Data.Sql.Builders.Mutations; " +
        "public sealed class ComputedBoundaryRow { public int Id { get; set; } public long LongValue { get; set; } public string Name { get; set; } } " +
        "public static class UnsupportedComputedBoundaryConsumer { " +
        "public static ISqlUpdateBuilder Apply(ISqlUpdateBuilder source, int captured) => " +
        "source.Returning<ComputedBoundaryRow>(row => new { Value = " + expression + " }); }";

    private static Expression<Func<RuntimeComputedBoundaryRow, object>> CreateUnsupportedRuntimeProjection(string shape)
    {
        var captured = 3;
        return shape switch
        {
            "checked" => row => new { Value = checked(row.Id + row.Id) },
            "captured" => row => new { Value = row.Id + captured },
            "constant" => row => new { Value = row.Id + 1 },
            "string" => row => new { Value = row.Name + row.Name },
            "mixed" => row => new { Value = row.Id + (long)row.LongValue },
            "cast" => row => new { Value = (long)(row.Id + row.Id) },
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null)
        };
    }

    private sealed class RuntimeComputedBoundaryRow
    {
        public int Id { get; set; }
        public long LongValue { get; set; }
        public string Name { get; set; }
    }

    private sealed class ComputedBoundaryMappingResolver : IEntityMappingResolver
    {
        public EntityDescriptor GetDescriptor(Type entityType) => throw new NotSupportedException();

        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext) => new()
        {
            EntityType = entityType,
            Columns = new Dictionary<string, ColumnMappingMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = new() { PropertyName = "Id", ColumnName = "Id" },
                ["LongValue"] = new() { PropertyName = "LongValue", ColumnName = "LongValue" },
                ["DecimalValue"] = new() { PropertyName = "DecimalValue", ColumnName = "DecimalValue" },
                ["OptionalCount"] = new() { PropertyName = "OptionalCount", ColumnName = "OptionalCount" },
                ["Name"] = new() { PropertyName = "Name", ColumnName = "Name" }
            }
        };
    }

    private sealed class ComputedBoundaryDialectProvider(ISqlProvider inner) : ISqlProvider, ISqlProviderProfileProvider
    {
        public string Key => inner.Key;
        public Bing.Data.Enums.DatabaseType DatabaseType => inner.DatabaseType;
        public IDialect Dialect { get; } = ComputedBoundaryAngleDialect.Instance;
        public ISqlClauseFactory ClauseFactory => inner.ClauseFactory;
        public ISqlTableReferenceParser TableReferenceParser => inner.TableReferenceParser;
        public ISqlPaginationRenderer PaginationRenderer => inner.PaginationRenderer;
        public IParameterManagerFactory ParameterManagerFactory => inner.ParameterManagerFactory;
        public IParamLiteralsResolver ParamLiteralsResolver => inner.ParamLiteralsResolver;
        public SqlProviderProfile Profile => ((ISqlProviderProfileProvider)inner).Profile;
    }

    private sealed class ComputedBoundaryAngleDialect : DialectBase
    {
        public static ComputedBoundaryAngleDialect Instance { get; } = new();
        public override char OpeningIdentifier => '<';
        public override char ClosingIdentifier => '>';
    }
}
