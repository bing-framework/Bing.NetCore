using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

/// <summary>查询生成器生成的实体元数据访问器测试。</summary>
public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>生成的映射应提供直接属性 Getter/Setter，并可绕过反射读写实体。</summary>
    [Fact]
    public void Generate_WhenEntityIsUsedByQuery_ShouldEmitGeneratedPropertyAccessors()
    {
        const string source = """
            using Bing.Data.Sql;

            public sealed class Row
            {
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }

            public static class Consumer
            {
                public static SqlLambdaQuery Build(SqlLambdaQuery query) => query.From<Row>();
            }
            """;

        var assembly = LoadGeneratedAssembly(RunGenerator(source));
        var rowType = assembly.GetType("Row")!;
        var metadata = new GeneratedEntityModelMetadataProvider().GetMetadata(rowType);
        var row = Activator.CreateInstance(rowType)!;
        var id = metadata.Properties["Id"];
        var name = metadata.Properties["Name"];

        Assert.NotNull(id.GeneratedGetter);
        Assert.NotNull(id.GeneratedSetter);
        Assert.NotNull(name.GeneratedGetter);
        Assert.NotNull(name.GeneratedSetter);

        id.GeneratedSetter(row, 7);
        name.GeneratedSetter(row, "generated");

        Assert.Equal(7, id.GeneratedGetter(row));
        Assert.Equal("generated", name.GeneratedGetter(row));
    }

    /// <summary>关键字属性名应可生成；私有 setter 和 init 属性不得生成运行时写入器。</summary>
    [Fact]
    public void Generate_WhenPropertyRequiresEscapingOrNonPublicWrite_ShouldPreserveMetadataContract()
    {
        const string source = """
            using Bing.Data.Sql;

            public sealed class Row
            {
                public int @class { get; set; }
                public int PrivateValue { get; private set; }
                public int InitValue { get; init; }
            }

            public static class Consumer
            {
                public static SqlLambdaQuery Build(SqlLambdaQuery query) => query.From<Row>();
            }
            """;

        var assembly = LoadGeneratedAssembly(RunGenerator(source));
        var rowType = assembly.GetType("Row")!;
        var metadata = new GeneratedEntityModelMetadataProvider().GetMetadata(rowType);

        var escaped = metadata.Properties["class"];
        var privateValue = metadata.Properties["PrivateValue"];
        var initValue = metadata.Properties["InitValue"];
        var row = Activator.CreateInstance(rowType)!;

        Assert.NotNull(escaped.GeneratedGetter);
        Assert.NotNull(escaped.GeneratedSetter);
        escaped.GeneratedSetter(row, 9);
        Assert.Equal(9, escaped.GeneratedGetter(row));

        Assert.True(privateValue.CanRead);
        Assert.False(privateValue.CanWrite);
        Assert.True(privateValue.HasNonPublicSetter);
        Assert.NotNull(privateValue.GeneratedGetter);
        Assert.Null(privateValue.GeneratedSetter);

        Assert.True(initValue.CanRead);
        Assert.True(initValue.CanWrite);
        Assert.False(initValue.HasNonPublicSetter);
        Assert.NotNull(initValue.GeneratedGetter);
        Assert.Null(initValue.GeneratedSetter);
    }
}
