using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
        await ExecuteAsync(connection, @"
Create Table If Not Exists Product(
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
    IsDeleted bit Not Null Default 0
);");
        await ExecuteAsync(connection, @"
Create Table If Not Exists ParameterSample(
    ParameterSampleId char(36) Not Null Primary Key,
    JsonValue longtext Null,
    DecimalValue decimal(24,6) Null,
    DateTimeValue datetime Null
);");
        await ExecuteAsync(connection, "Drop Procedure If Exists Proc_GetProductCode_Output;");
        await ExecuteAsync(connection, @"
Create Procedure Proc_GetProductCode_Output(IN id char(36), OUT code_output varchar(50))
Begin
    Select Code Into code_output From Product Where ProductId = id;
End;");
        await ExecuteAsync(connection, "Drop Procedure If Exists Proc_AppendProductCode_InputOutput;");
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

    /// <summary>
    /// 初始化
    /// </summary>
    public static void InitProcedures(DatabaseFacade database)
    {
        CreateGetProductCodeProcedure(database);
        CreateGetProductProcedure(database);
        CreateGetProductsProcedure(database);
        CreateGetOrderItemProcedure(database);
        CreateGetProductCodeOutputProcedure(database);
        CreateInsertProductProcedure(database);
    }

    /// <summary>
    /// 创建Proc_GetProductCode存储过程
    /// </summary>
    private static void CreateGetProductCodeProcedure(DatabaseFacade database)
    {
        var sql = @"
                Create Procedure Proc_GetProductCode (
	                id char(36)
                )
                Begin
                    Select Code
                    From Product
                    Where ProductId = id;
                End
            ";
        database.ExecuteSqlRaw(sql);
    }

    /// <summary>
    /// 创建Proc_GetProduct存储过程
    /// </summary>
    private static void CreateGetProductProcedure(DatabaseFacade database)
    {
        var sql = @"
                Create Procedure Proc_GetProduct (
	                id char(36)
                )
                Begin
                    Select *
                    From Product
                    Where ProductId = id;
                End
            ";
        database.ExecuteSqlRaw(sql);
    }

    /// <summary>
    /// 创建Proc_GetProducts存储过程
    /// </summary>
    private static void CreateGetProductsProcedure(DatabaseFacade database)
    {
        var sql = @"
                Create Procedure Proc_GetProducts (
	                id char(36),
                    id2 char(36)
                )
                Begin
                    Select p.ProductId As Id,p.*
                    From Product As p
                    Where p.ProductId in ( id,id2 );
                End
            ";
        database.ExecuteSqlRaw(sql);
    }

    /// <summary>
    /// 创建Proc_GetOrderItem存储过程
    /// </summary>
    private static void CreateGetOrderItemProcedure(DatabaseFacade database)
    {
        var sql = @"
                Create Procedure Proc_GetOrderItem (
	                id char(36)
                )
                Begin
                    Select i.OrderItemId As Id,i.*,o.OrderId As Id,o.*
                    From OrderItem As i
                    Left Join `Order` As o On o.OrderId=i.OrderId
                    Where i.OrderItemId=id;
                End
            ";
        database.ExecuteSqlRaw(sql);
    }

    /// <summary>
    /// 创建Proc_GetProductCode_Output存储过程
    /// </summary>
    private static void CreateGetProductCodeOutputProcedure(DatabaseFacade database)
    {
        var sql = @"
                Create Procedure Proc_GetProductCode_Output (
	                id char(36),
                    out code_output varchar(50)
                )
                Begin
                    Select `Code` into code_output
                    From `Product`
                    Where `ProductId` = id;
                End
            ";
        database.ExecuteSqlRaw(sql);
    }

    /// <summary>
    /// 创建Proc_InsertProduct存储过程
    /// </summary>
    private static void CreateInsertProductProcedure(DatabaseFacade database)
    {
        var sql = @"
                Create Procedure Proc_InsertProduct (
	                id char(36),
                    codeValue varchar(50)
                )
                Begin
                    Insert Into Product(ProductId,Code)
                    Values( id,codeValue );
                End
            ";
        database.ExecuteSqlRaw(sql);
    }
}
