using MySqlConnector;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// 数据库初始化脚本
/// </summary>
public class DatabaseScript
{
    /// <summary>
    /// 初始化 MySQL 集成测试所需的表和存储过程。
    /// </summary>
    /// <param name="connection">已打开的 MySQL 连接。</param>
    /// <returns>异步任务。</returns>
    public static async Task InitializeAsync(MySqlConnection connection)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        await ExecuteAsync(connection, "Drop Procedure If Exists Proc_GetProductCode_Output;");
        await ExecuteAsync(connection, "Drop Procedure If Exists Proc_AppendProductCode_InputOutput;");
        await ExecuteAsync(connection, "Drop Table If Exists `Merchants.Company`;");
        await ExecuteAsync(connection, "Drop Table If Exists ParameterSample;");
        await ExecuteAsync(connection, "Drop Table If Exists Product;");
        await ExecuteAsync(connection, @"
    Create Table Product(
    ProductId char(36) Not Null Primary Key,
    Code varchar(50) Not Null,
    Name varchar(200) Null,
    Price decimal(12,2) Not Null Default 0,
    IntPrice int Not Null Default 0,
    LongPrice bigint Not Null Default 0,
    FloatPrice float Not Null Default 0,
    Description varchar(500) Null,
    Enabled bit Not Null Default 1,
    CreationTime datetime Null,
    CreatorId varchar(36) Null,
    LastModificationTime datetime Null,
    LastModifierId varchar(36) Null,
    IsDeleted bit Not Null Default 0,
    Version binary(8) Null
);");
        await ExecuteAsync(connection, @"
Create Table ParameterSample(
    ParameterSampleId char(36) Not Null Primary Key,
    JsonValue longtext Null,
    DecimalValue decimal(24,6) Null,
    DateTimeValue datetime Null
);");
        await ExecuteAsync(connection, @"
    Create Table `Merchants.Company`(
    CompanyId char(36) Not Null Primary Key,
    Name varchar(200) Not Null
);");
        await ExecuteAsync(connection, @"
Create Procedure Proc_GetProductCode_Output(IN id char(36), OUT code_output varchar(50))
Begin
    Select Code Into code_output From Product Where ProductId = id;
End;");
        await ExecuteAsync(connection, @"
Create Procedure Proc_AppendProductCode_InputOutput(INOUT code_value varchar(50))
Begin
    Set code_value = Concat(code_value, '_output');
End;");
    }

    /// <summary>
    /// 清理 MySQL 集成测试数据，保留表结构和存储过程。
    /// </summary>
    /// <param name="connection">已打开的 MySQL 连接。</param>
    /// <returns>异步任务。</returns>
    public static async Task ResetAsync(MySqlConnection connection)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        await ExecuteAsync(connection, "Delete From `Merchants.Company`;");
        await ExecuteAsync(connection, "Delete From ParameterSample;");
        await ExecuteAsync(connection, "Delete From Product;");
    }

    /// <summary>
    /// 执行 MySQL 初始化脚本。
    /// </summary>
    /// <param name="connection">已打开的 MySQL 连接。</param>
    /// <param name="sql">SQL 脚本。</param>
    /// <returns>异步任务。</returns>
    private static async Task ExecuteAsync(MySqlConnection connection, string sql)
    {
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

}
