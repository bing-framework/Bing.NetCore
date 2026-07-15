# 阶段四：Query/Executor Factory 收敛

## 完成项

- 确认 `ISqlQueryFactory` 与 `ISqlExecutorFactory` 只保留无参数和 `dbKey` 两个创建入口。
- `SqlFactoryBase` 仍在一次创建流程中固定数据源描述、数据库类型、Provider 实现类型、连接配置与数据库上下文。
- 命名连接字符串解析失败时不再回退到 Provider 模板连接字符串，避免数据源配置错误被静默掩盖。
- 保留显式外部 `IDbConnection` 模板场景：外部连接由调用方提供时，Factory 不要求额外连接字符串。

## 测试

新增并通过：命名连接字符串不存在时，Factory 创建 Query 必须抛出包含数据源 key 与连接字符串名称的异常。

## 验证

- `dotnet build .\framework\src\Bing.Dapper.Core\Bing.Dapper.Core.csproj -nologo -v minimal`：通过。
- `dotnet test .\framework\tests\Bing.Dapper.SqlServer.Tests\Bing.Dapper.SqlServer.Tests.csproj -nologo -v minimal`：170 passed。

## 风险

- Provider 单独注册、重复注册与多 Provider 共存的公共服务完整性将在阶段十四统一处理。

## 2026-07-10 复核

- `ISqlQueryFactory` 与 `ISqlExecutorFactory` 仍仅保留无参和 `dbKey` 创建入口。
- Factory 按数据源描述精确解析命名连接字符串；缺失命名配置不再回退 `Default`。
- `Bing.Dapper.SqlServer.Tests` 已增至 174 passed，其中包含 Factory 连接字符串错误及事务 API 回归。
