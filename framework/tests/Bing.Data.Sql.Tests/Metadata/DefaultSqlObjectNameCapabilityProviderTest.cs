using Bing.Data.Enums;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>
/// <see cref="DefaultSqlObjectNameCapabilityProvider"/> 单元测试。
/// </summary>
public class DefaultSqlObjectNameCapabilityProviderTest
{
    /// <summary>
    /// 测试目的：验证每个受支持 Provider 应返回精确的对象名称能力合同。
    /// </summary>
    [Theory]
    [InlineData(DatabaseType.MySql, false, true, 2)]
    [InlineData(DatabaseType.Doris, false, true, 2)]
    [InlineData(DatabaseType.SqlServer, true, true, 3)]
    [InlineData(DatabaseType.PgSql, false, true, 2)]
    [InlineData(DatabaseType.Oracle, false, true, 2)]
    [InlineData(DatabaseType.Sqlite, false, false, 1)]
    public void GetCapabilities_WhenProviderIsSupported_ShouldReturnExactContract(DatabaseType databaseType, bool supportsDatabase, bool supportsSchema, int maximumNameParts)
    {
        // Arrange
        var provider = new DefaultSqlObjectNameCapabilityProvider();

        // Act
        var capabilities = provider.GetCapabilities(databaseType);

        // Assert
        Assert.Equal(supportsDatabase, capabilities.SupportsDatabase);
        Assert.Equal(supportsSchema, capabilities.SupportsSchema);
        Assert.Equal(maximumNameParts, capabilities.MaximumNameParts);
    }

    /// <summary>
    /// 测试目的：未配置或未知 Provider 时应拒绝返回不确定的能力合同。
    /// </summary>
    [Fact]
    public void GetCapabilities_WhenProviderIsMissingOrUnknown_ShouldThrowNotSupportedException()
    {
        // Arrange
        var provider = new DefaultSqlObjectNameCapabilityProvider();

        // Act and Assert
        Assert.Throws<NotSupportedException>(() => provider.GetCapabilities(null));
        Assert.Throws<NotSupportedException>(() => provider.GetCapabilities((DatabaseType)int.MaxValue));
    }
}