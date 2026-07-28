using Bing.Data.Enums;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>
/// <see cref="DefaultSqlCrossDatabaseQueryValidator"/> 单元测试。
/// </summary>
public class DefaultSqlCrossDatabaseQueryValidatorTest
{
    /// <summary>
    /// 测试目的：支持 Database 限定的 Provider 应允许结构化跨数据库查询。
    /// </summary>
    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.Doris)]
    public void Validate_WhenProviderSupportsDatabaseQualifiedReferences_ShouldSucceed(DatabaseType databaseType)
    {
        // Arrange
        var validator = new DefaultSqlCrossDatabaseQueryValidator();
        var context = CreateContext(databaseType);
        var source = new SqlTableReference { Database = "primary", TableName = "users" };
        var target = new SqlTableReference { Database = "reporting", TableName = "orders" };

        // Act
        var exception = Record.Exception(() => validator.Validate(context, source, target));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// 测试目的：PostgreSQL 在源表或目标表包含 Database 限定时应拒绝普通跨库查询。
    /// </summary>
    [Fact]
    public void Validate_WhenPostgreSqlReferenceContainsDatabase_ShouldThrowNotSupportedException()
    {
        // Arrange
        var validator = new DefaultSqlCrossDatabaseQueryValidator();
        var context = CreateContext(DatabaseType.PgSql);
        var source = new SqlTableReference { TableName = "users" };
        var target = new SqlTableReference { Database = "reporting", TableName = "orders" };

        // Act
        var action = () => validator.Validate(context, source, target);

        // Assert
        Assert.Throws<NotSupportedException>(action);
    }

    /// <summary>
    /// 测试目的：Oracle 对原始 From 的带 Database Join 目标也应拒绝。
    /// </summary>
    [Fact]
    public void ValidateTarget_WhenOracleTargetContainsDatabase_ShouldThrowNotSupportedException()
    {
        // Arrange
        var validator = new DefaultSqlCrossDatabaseQueryValidator();
        var context = CreateContext(DatabaseType.Oracle);
        var target = new SqlTableReference { Database = "reporting", TableName = "orders" };

        // Act
        var action = () => validator.ValidateTarget(context, target);

        // Assert
        Assert.Throws<NotSupportedException>(action);
    }

    /// <summary>
    /// 测试目的：SQLite 对带 Schema 的结构化 Join 应拒绝，避免生成不受支持的名称。
    /// </summary>
    [Fact]
    public void Validate_WhenSqliteReferenceContainsSchema_ShouldThrowNotSupportedException()
    {
        // Arrange
        var validator = new DefaultSqlCrossDatabaseQueryValidator();
        var context = CreateContext(DatabaseType.Sqlite);
        var source = new SqlTableReference { TableName = "users" };
        var target = new SqlTableReference { Schema = "main", TableName = "orders" };

        // Act
        var action = () => validator.Validate(context, source, target);

        // Assert
        Assert.Throws<NotSupportedException>(action);
    }

    /// <summary>
    /// 测试目的：缺少执行上下文时应跳过 Provider 能力校验。
    /// </summary>
    [Fact]
    public void Validate_WhenExecutionContextIsMissing_ShouldSkipCapabilityValidation()
    {
        // Arrange
        var validator = new DefaultSqlCrossDatabaseQueryValidator();
        var source = new SqlTableReference { Database = "primary", TableName = "users" };
        var target = new SqlTableReference { Database = "reporting", Schema = "archive", TableName = "orders" };

        // Act
        var exception = Record.Exception(() => validator.Validate(null, source, target));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// 测试目的：目标表引用为空时应拒绝执行校验。
    /// </summary>
    [Fact]
    public void Validate_WhenTargetIsMissing_ShouldThrowArgumentNullException()
    {
        // Arrange
        var validator = new DefaultSqlCrossDatabaseQueryValidator();

        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => validator.Validate(CreateContext(DatabaseType.SqlServer), null, null));
    }

    /// <summary>
    /// 创建指定数据库类型的执行上下文。
    /// </summary>
    /// <param name="databaseType">数据库类型。</param>
    /// <returns>包含数据库类型的数据源上下文。</returns>
    private static DatabaseContext CreateContext(DatabaseType databaseType) => new()
    {
        DataSource = new SqlDataSourceDescriptor { DatabaseType = databaseType }
    };
}