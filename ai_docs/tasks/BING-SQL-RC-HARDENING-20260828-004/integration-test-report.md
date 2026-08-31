# 集成测试报告

## 状态

`PARTIAL`。SQLite 已在本地真实执行；三个外部 Provider 尚无本任务的 real non-skip 证据。

| Provider | 当前状态 | 完成所需证据 |
| --- | --- | --- |
| SQLite | COMPLETED | `Bing.Dapper.Sqlite.Tests.Integration`：net8.0 151 passed/0 failed/0 skipped，56 s；net6.0 151 passed/0 failed/0 skipped，56 s。 |
| MySQL | BLOCKED | 受保护 job 的专属 gate、专属连接变量、reset 授权、安全测试库、non-skip TRX/JSON。 |
| PostgreSQL | BLOCKED | 受保护 job 的专属 gate、专属连接变量、reset 授权、安全测试库、non-skip TRX/JSON。 |
| SQL Server | BLOCKED | 受保护 job 的专属 gate、专属连接变量、reset 授权、安全测试库、non-skip TRX/JSON。 |

默认 gate Skip、DI Startup 测试、runner `-SelfTest` 和 `-ValidateOnly` 不计入真实 Provider 通过。

## 已执行命令

```powershell
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -f net8.0 --no-restore --nologo -v quiet
dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests.Integration\Bing.Dapper.Sqlite.Tests.Integration.csproj -c Release -f net6.0 --no-restore --nologo -v quiet
```

net8.0 运行产生 `NETSDK1206`（SQLitePCLRaw 的既有 alpine-x64 RID 资产）warning，测试仍实际通过。恢复外部 Provider 验收需要维护者在受保护环境中提供各自专属 gate、连接变量、`ALLOW_DATABASE_RESET_FOR_TESTS=true` 和安全测试数据库，再通过 runner 生成 current non-skip TRX/JSON。
