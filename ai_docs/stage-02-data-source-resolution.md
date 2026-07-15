# 阶段二：数据源配置单一化

## 完成项

- `DefaultSqlDataSourceResolver` 对显式 `dbKey` 只执行精确解析，缺失时立即抛出包含请求 key 与已配置 key 的异常。
- 未传入 `dbKey` 时，优先使用实际存在的默认数据源。
- 默认数据源键未命中且仅配置一个数据源时，解析唯一数据源；这支持唯一数据源不使用 `default` 命名的场景。
- 多数据源且没有可用默认数据源时，抛出包含缺失配置字段的明确异常。
- 取消数据源描述解析阶段的连接字符串必填校验，使数据库作用域和映射解析能够处理尚未执行的上下文；连接字符串仍由 Query/Executor 工厂在创建执行对象时解析。

## 测试

新增并通过以下 SQL 核心测试：

- 未指定数据源时使用默认数据源。
- 仅配置唯一数据源时使用唯一数据源。
- 多数据源但缺失默认数据源时抛出异常。

## 验证

- `dotnet build .\framework\src\Bing.Data.Sql\Bing.Data.Sql.csproj -nologo -v minimal`：通过。
- `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -nologo -v minimal`：916 passed。

## 风险

- Factory 的连接字符串优先级及外部 `IDbConnection` 模板处理将在阶段四随 Factory 收敛完成验证。

## 2026-07-10 收敛

- `SqlMetadataOptions.DefaultDatabaseContext` 不再预写 `Default` 连接名称，未指定 `dbKey` 时可正确进入默认数据源或唯一数据源解析。
- `DefaultSqlDatabaseContextResolver`、实体映射和参数工厂不再构造伪 SQL Server 数据源。
- SQL Factory 与 Query 使用 `ConnectionStringCollection` 的精确键查询；描述符指定的连接名称不存在时明确抛错，不回退 `Default` 连接字符串或模板连接字符串。
- Provider 注册不会再为缺少连接配置的数据源隐式填充 `Default` 连接名称。

验证：`Bing.Data.Sql.Tests` 920 passed；`Bing.Dapper.MySql.Tests` 156 passed；`Bing.Dapper.SqlServer.Tests` 172 passed；`Bing.Dapper.PostgreSql.Tests` 124 passed；`Bing.Dapper.Sqlite.Tests` 4 passed；`Bing.Dapper.Oracle.Tests` 106 passed。
