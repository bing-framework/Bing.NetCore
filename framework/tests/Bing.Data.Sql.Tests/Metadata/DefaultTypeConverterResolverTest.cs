using System.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql.Metadata;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>
/// <see cref="DefaultTypeConverterResolver"/> 单元测试。
/// </summary>
public class DefaultTypeConverterResolverTest
{
    /// <summary>
    /// 测试目的：未注册转换器时应返回 null。
    /// </summary>
    [Fact]
    public void Resolve_WhenNoRegistrationExists_ShouldReturnNull()
    {
        // Arrange
        var resolver = new DefaultTypeConverterResolver();

        // Act
        var result = resolver.Resolve(DatabaseType.SqlServer);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// 测试目的：有效注册应按数据库类型返回对应转换器。
    /// </summary>
    [Fact]
    public void Resolve_WhenRegistrationExists_ShouldReturnMatchingConverter()
    {
        // Arrange
        var converter = new Mock<ITypeConverter>().Object;
        var resolver = new DefaultTypeConverterResolver(new[]
        {
            new DatabaseTypeConverterRegistration { DatabaseType = DatabaseType.MySql, Converter = converter }
        });

        // Act
        var result = resolver.Resolve(DatabaseType.MySql);

        // Assert
        Assert.Same(converter, result);
    }

    /// <summary>
    /// 测试目的：同一数据库类型重复注册时最后一个非空转换器应生效。
    /// </summary>
    [Fact]
    public void Resolve_WhenRegistrationRepeated_ShouldUseLatestNonNullConverter()
    {
        // Arrange
        var firstConverter = new Mock<ITypeConverter>().Object;
        var latestConverter = new Mock<ITypeConverter>().Object;
        var resolver = new DefaultTypeConverterResolver(new[]
        {
            new DatabaseTypeConverterRegistration { DatabaseType = DatabaseType.PgSql, Converter = firstConverter },
            null,
            new DatabaseTypeConverterRegistration { DatabaseType = DatabaseType.PgSql, Converter = null },
            new DatabaseTypeConverterRegistration { DatabaseType = DatabaseType.PgSql, Converter = latestConverter }
        });

        // Act
        var result = resolver.Resolve(DatabaseType.PgSql);

        // Assert
        Assert.Same(latestConverter, result);
    }
}