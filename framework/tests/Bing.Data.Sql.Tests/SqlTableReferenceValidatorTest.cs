using Bing.Data.Enums;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// SQL 表引用验证器测试。
/// </summary>
public class SqlTableReferenceValidatorTest
{
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
}