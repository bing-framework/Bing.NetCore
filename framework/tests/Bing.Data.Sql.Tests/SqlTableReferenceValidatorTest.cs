using Bing.Data.Enums;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// SQL 表引用验证器测试。
/// </summary>
public class SqlTableReferenceValidatorTest
{
    /// <summary>
    /// 测试目的：空表名应在格式化前被拒绝。
    /// </summary>
    [Fact]
    public void Validate_WhenTableNameIsMissing_ShouldThrowArgumentException()
    {
        // Arrange
        var validator = new DefaultSqlTableReferenceValidator();
        var reference = new SqlTableReference();

        // Act
        var action = () => validator.Validate(reference, DatabaseType.SqlServer);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    /// <summary>
    /// 测试目的：SQL Server 的 Database、Schema 和 TableName 三段名称应通过验证。
    /// </summary>
    [Fact]
    public void Validate_WhenSqlServerReferenceHasThreeParts_ShouldSucceed()
    {
        // Arrange
        var validator = new DefaultSqlTableReferenceValidator();
        var reference = new SqlTableReference { Database = "erp", Schema = "dbo", TableName = "orders" };

        // Act
        var exception = Record.Exception(() => validator.Validate(reference, DatabaseType.SqlServer));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// 测试目的：危险表名必须在格式化前被拒绝。
    /// </summary>
    [Fact]
    public void Validate_WhenTableNameContainsStatementDelimiter_ShouldThrowArgumentException()
    {
        // Arrange
        var validator = new DefaultSqlTableReferenceValidator();
        var reference = new SqlTableReference { TableName = "orders;drop table users" };

        // Act
        var action = () => validator.Validate(reference, DatabaseType.MySql);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    /// <summary>
    /// 测试目的：SQLite 不支持 Schema 限定，应在 SQL 生成前拒绝该表引用。
    /// </summary>
    [Fact]
    public void Validate_WhenSqliteReferenceContainsSchema_ShouldThrowNotSupportedException()
    {
        // Arrange
        var validator = new DefaultSqlTableReferenceValidator();
        var reference = new SqlTableReference { Schema = "main", TableName = "orders" };

        // Act
        var action = () => validator.Validate(reference, DatabaseType.Sqlite);

        // Assert
        Assert.Throws<NotSupportedException>(action);
    }

    /// <summary>
    /// 测试目的：PostgreSQL 不支持 Database 限定，应拒绝三段表引用。
    /// </summary>
    [Fact]
    public void Validate_WhenPostgreSqlReferenceContainsDatabase_ShouldThrowNotSupportedException()
    {
        // Arrange
        var validator = new DefaultSqlTableReferenceValidator();
        var reference = new SqlTableReference { Database = "reporting", Schema = "public", TableName = "orders" };

        // Act
        var action = () => validator.Validate(reference, DatabaseType.PgSql);

        // Assert
        Assert.Throws<NotSupportedException>(action);
    }

    /// <summary>
    /// 测试目的：动态别名包含换行符时应被拒绝，避免拼接危险标识符。
    /// </summary>
    [Fact]
    public void Validate_WhenAliasContainsNewLine_ShouldThrowArgumentException()
    {
        // Arrange
        var validator = new DefaultSqlTableReferenceValidator();
        var reference = new SqlTableReference { TableName = "orders", Alias = "order\nitems" };

        // Act
        var action = () => validator.Validate(reference, DatabaseType.MySql);

        // Assert
        Assert.Throws<ArgumentException>(action);
    }

    /// <summary>数据库、架构、表名及别名的每种非法字符都必须拒绝。</summary>
    [Theory]
    [InlineData("database", '\r')]
    [InlineData("database", '\n')]
    [InlineData("database", ';')]
    [InlineData("schema", '\r')]
    [InlineData("schema", '\n')]
    [InlineData("schema", ';')]
    [InlineData("table", '\r')]
    [InlineData("table", '\n')]
    [InlineData("table", ';')]
    [InlineData("alias", '\r')]
    [InlineData("alias", '\n')]
    [InlineData("alias", ';')]
    public void Validate_WhenAnyNamePartContainsInvalidCharacter_ShouldThrow(string part, char invalid)
    {
        var value = $"name{invalid}suffix";
        var reference = new SqlTableReference
        {
            Database = part == "database" ? value : "db",
            Schema = part == "schema" ? value : "dbo",
            TableName = part == "table" ? value : "orders",
            Alias = part == "alias" ? value : "o"
        };

        var validator = new DefaultSqlTableReferenceValidator();
        Assert.Throws<ArgumentException>(() => validator.Validate(reference, DatabaseType.SqlServer));
    }
}
