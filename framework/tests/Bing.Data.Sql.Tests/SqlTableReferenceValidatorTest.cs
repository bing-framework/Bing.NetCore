using Bing.Data.Enums;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// SQL 表引用验证器测试。
/// </summary>
public class SqlTableReferenceValidatorTest
{
    /// <summary>
    /// 测试 - 同一 Provider 应复用不可变对象名称能力实例。
    /// </summary>
    [Fact]
    public void GetCapabilities_WhenProviderIsTheSame_ShouldReuseImmutableInstance()
    {
        // Arrange
        var provider = new DefaultSqlObjectNameCapabilityProvider();

        // Act
        var first = provider.GetCapabilities(DatabaseType.SqlServer);
        var second = provider.GetCapabilities(DatabaseType.SqlServer);

        // Assert
        Assert.Same(first, second);
    }

    /// <summary>
    /// 测试 - SQL Server 三段对象名称应通过验证。
    /// </summary>
    [Fact]
    public void Validate_WhenSqlServerReferenceHasThreeParts_ShouldSucceed()
    {
        // Arrange
        var validator = new DefaultSqlTableReferenceValidator();
        var reference = new SqlTableReference
        {
            Catalog = "erp",
            PhysicalSchema = "dbo",
            ResolvedTableName = "orders"
        };

        // Act
        var action = () => validator.Validate(reference, DatabaseType.SqlServer);

        // Assert
        Assert.Null(Record.Exception(action));
    }

    /// <summary>
    /// 测试 - 表引用超过名称段数上限应失败。
    /// </summary>
    [Fact]
    public void Validate_WhenReferenceExceedsMaximumParts_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var validator = new DefaultSqlTableReferenceValidator(new FixedCapabilityProvider(new SqlObjectNameCapabilities
        {
            SupportsCatalog = true,
            SupportsPhysicalSchema = true,
            MaximumNameParts = 2
        }));
        var reference = new SqlTableReference
        {
            Catalog = "catalog",
            PhysicalSchema = "schema",
            ResolvedTableName = "orders",
        };

        // Act
        var action = () => validator.Validate(reference, DatabaseType.SqlServer);

        // Assert
        Assert.Throws<InvalidOperationException>(action);
    }

    /// <summary>
    /// 测试 - 危险标识符应在格式化前被拒绝。
    /// </summary>
    [Fact]
    public void Validate_WhenReferenceContainsStatementDelimiter_ShouldThrowArgumentException()
    {
        // Arrange
        var validator = new DefaultSqlTableReferenceValidator();
        var reference = new SqlTableReference
        {
            ResolvedTableName = "orders;drop table users"
        };

        // Act
        var action = () => validator.Validate(reference, DatabaseType.MySql);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    /// <summary>
    /// 固定名称能力提供器。
    /// </summary>
    private sealed class FixedCapabilityProvider : ISqlObjectNameCapabilityProvider
    {
        private readonly SqlObjectNameCapabilities _capabilities;

        /// <summary>
        /// 初始化一个<see cref="FixedCapabilityProvider"/>类型的实例。
        /// </summary>
        /// <param name="capabilities">固定能力。</param>
        public FixedCapabilityProvider(SqlObjectNameCapabilities capabilities) => _capabilities = capabilities;

        /// <inheritdoc />
        public SqlObjectNameCapabilities GetCapabilities(DatabaseType? databaseType) => _capabilities;
    }
}