using Microsoft.Data.SqlClient;

namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// SQL Server 聚合集成测试数据库脚本。
/// </summary>
internal static class DatabaseScript
{
    /// <summary>
    /// 初始化固定前缀的聚合集成测试表。
    /// </summary>
    /// <param name="connection">已打开的 SQL Server 连接。</param>
    /// <returns>异步任务。</returns>
    public static async Task InitializeAsync(SqlConnection connection)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        await ExecuteAsync(connection, @"
If Object_Id(N'dbo.BingSqlAggregateIntegration', N'U') Is Not Null
    Drop Table dbo.BingSqlAggregateIntegration;
If Object_Id(N'dbo.BingSqlHierarchyIntegration', N'U') Is Not Null
    Drop Table dbo.BingSqlHierarchyIntegration;
If Object_Id(N'dbo.BingSqlContractIntegration', N'U') Is Not Null
    Drop Table dbo.BingSqlContractIntegration;
If Object_Id(N'dbo.BingSqlBatchIntegration', N'U') Is Not Null
    Drop Table dbo.BingSqlBatchIntegration;
If Object_Id(N'dbo.BingSqlContractProcedure', N'P') Is Not Null
    Drop Procedure dbo.BingSqlContractProcedure;
Create Table dbo.BingSqlAggregateIntegration(
    Id int Identity(1,1) Not Null Primary Key,
    UserId nvarchar(50) Null,
    Amount decimal(18,2) Null,
    Enabled bit Not Null
);
Create Table dbo.BingSqlHierarchyIntegration(
    Id int Not Null Primary Key,
    ParentId int Null,
    Name nvarchar(100) Not Null,
    Index IX_BingSqlHierarchyIntegration_ParentId(ParentId)
);
Create Table dbo.BingSqlContractIntegration(
    Id int Identity(1,1) Not Null Primary Key,
    Code nvarchar(100) Not Null,
    NullableValue int Null,
    Amount decimal(18,4) Not Null,
    CreatedAt datetime2(3) Not Null,
    Enabled bit Not Null
);
Create Table dbo.BingSqlBatchIntegration(
    Id int Identity(1,1) Not Null Primary Key,
    Code nvarchar(100) Not Null Unique,
    Name nvarchar(100) Null,
    Enabled bit Not Null Default 1
);
Exec(N'Create Procedure dbo.BingSqlContractProcedure
    @InputValue int,
    @InputOutputValue int Output,
    @OutputValue int Output
As
Begin
    Set NoCount On;
    Set @OutputValue = @InputValue + 1;
    Set @InputOutputValue = @InputOutputValue + @InputValue;
    Select @OutputValue As OutputValue;
    Return @InputValue + 2;
End');
" );
    }

    /// <summary>
    /// 清空固定聚合集成测试表的数据。
    /// </summary>
    /// <param name="connection">已打开的 SQL Server 连接。</param>
    /// <returns>异步任务。</returns>
    public static Task ResetAsync(SqlConnection connection) =>
        ExecuteAsync(connection, "Delete From dbo.BingSqlBatchIntegration; Delete From dbo.BingSqlContractIntegration; Delete From dbo.BingSqlHierarchyIntegration; Delete From dbo.BingSqlAggregateIntegration;");

    /// <summary>
    /// 写入覆盖 null、重复值和条件表达式的聚合测试数据。
    /// </summary>
    /// <param name="connection">已打开的 SQL Server 连接。</param>
    /// <returns>异步任务。</returns>
    public static Task SeedAggregateDataAsync(SqlConnection connection) => ExecuteAsync(connection, @"
Insert Into dbo.BingSqlAggregateIntegration(UserId, Amount, Enabled) Values
(N'A', 10.00, 1),
(N'A', 10.00, 1),
(N'B', 20.00, 0),
(Null, Null, 1);");

    /// <summary>
    /// 执行 SQL Server 集成测试脚本。
    /// </summary>
    /// <param name="connection">已打开的 SQL Server 连接。</param>
    /// <param name="sql">待执行的 SQL。</param>
    /// <returns>异步任务。</returns>
    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}