# 阶段五：DiagnosticsMessage 单模型化

## 完成项

- `DiagnosticsMessage.Parameters` 现在是唯一的 `SqlParameterDiagnosticSnapshot`。
- 删除公共诊断模型中的原始参数、Dapper 绑定参数、重复参数元数据、旧参数快照名称，以及顶层数据库类型和数据库字段。
- 参数快照仅包含参数项、原始参数类型名称和是否通过元数据绑定；不保存 Dapper 内部对象。
- 新增连接来源枚举，连接快照包含 `DbKey`、数据库类型、来源、所有权、只读标识和读取偏好。
- 事务快照包含事务 ID、所有权和主库短事务标识。
- 删除未使用的 `SqlQueryDiagnosticBeforeMessage`、`SqlQueryDiagnosticAfterMessage` 和 `SqlQueryDiagnosticErrorMessage` 旁路模型。
- 参数项仅保留诊断契约需要的值、敏感标识和绑定元数据，不再暴露 Provider、字段存储或转换器实现细节。
- SkyAPM 只消费 `Parameters`、`Connection`、`Transaction`，已移除旧字段 fallback。
- SkyAPM 将连接、读取偏好和事务所有权写入标签，敏感参数始终使用 `?`。
- 敏感参数在诊断快照中不保留值或原始值。

## 验证

- `dotnet build .\framework\src\Bing.Data.Sql\Bing.Data.Sql.csproj -nologo -v minimal`：通过。
- `dotnet build .\framework\src\Bing.Dapper.Core\Bing.Dapper.Core.csproj -nologo -v minimal`：通过。
- `dotnet build .\framework\src\Bing.Extensions.SkyApm.Diagnostics.Sql\Bing.Extensions.SkyApm.Diagnostics.Sql.csproj -nologo -v minimal`：通过。
- `dotnet test .\framework\tests\Bing.Dapper.SqlServer.Tests\Bing.Dapper.SqlServer.Tests.csproj -nologo -v minimal`：178 passed。
- 对生产源码搜索旧诊断旁路类型和反射克隆连接：无残留。

## 历史风险（已闭环）

- EF Core Shared 与 Independent 的连接来源、资源所有权和事务快照已由本地 SQLite 执行测试直接断言。
- 独立事务作用域与主库短事务的诊断事务信息已由 SQL Server 捕获式测试覆盖；主库短事务在 `BeforeExecute` 创建前已绑定为自有事务。

## 2026-07-10 复核

- 诊断模型继续仅使用统一参数、连接和事务快照，未重新引入旧参数字段。
- EF Shared 与 Independent 连接来源、事务快照的专项断言将随诊断消费者的 Integration Test 一并补充。

## 2026-07-16 流式查询收尾

- `StreamQuery<T>` 和 `StreamQueryAsync<T>` 在完整枚举、提前终止时都会完成一次 `AfterExecute` 诊断；异常路径仅发布 `ErrorExecute`。
- 流式枚举提前终止仍释放 Reader，保留现有 `buffered:false` 列表查询语义：列表 API 会物化最终结果，真正逐行读取继续使用流式 API。
- SQL Server Provider 测试新增提前终止流式枚举的单次完成诊断断言。

## 2026-07-17 事件快照隔离

- `BeforeExecute`、`AfterExecute` 与 `ErrorExecute` 分别发布深拷贝诊断载荷；后续事件不会修改订阅方已接收的 `BeforeExecute` 对象。
- 每个事件保留相同的 `ExecutionId`、SQL、参数、连接和事务快照，`AfterExecute` 只在自己的载荷上补充耗时，`ErrorExecute` 只在自己的载荷上补充异常。
- SQL Server 捕获式 Provider 测试验证 Before/After 对象、参数、连接和事务快照均不共享引用。

## 本轮补充

- SQLite 真实集成测试验证 Query 创建后即固定诊断上下文；后续 Ambient `dbKey` 切换不会改变事件中的 `DbKey` 或 Mapping Profile。
- 显式启用时才记录 TenantId 的行为保持不变。

## 2026-08-06 事件订阅与 EF Core 资源覆盖

- `BeforeExecute`、`AfterExecute` 和 `ErrorExecute` 任一事件被订阅时都会创建一次共享执行快照；仅启用完成或异常事件的消费者不再因未订阅 `BeforeExecute` 而丢失诊断。
- SQL Server 捕获式测试通过 `DiagnosticListener.Subscribe` 的谓词订阅分别验证仅订阅完成和异常事件时的发布行为。
- EF Core SQLite 本地测试验证 Shared 模式将 EF 连接与当前 EF 事务标记为 `External` / `EntityFrameworkCore`；Independent 模式即使 EF 事务存在也保持 `Owned` / `DataSource` 连接且不绑定 EF 事务。
