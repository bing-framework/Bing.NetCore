# 单元测试报告

## 状态

`COMPLETED`。以下均为本任务基线 HEAD `faba0eee924b7c992dc0aaad414099d92308f5f9` 上实施后的本地实际结果。

## 实际结果

| 项目/合同 | TFM | 结果 |
| --- | --- | --- |
| `Bing.Data.Sql.Tests` | `net6.0` | 1265 passed, 0 failed, 0 skipped, 739 ms |
| `Bing.Data.Sql.Tests` | `net8.0` | 1265 passed, 0 failed, 0 skipped, 735 ms |
| `Bing.Data.Sql.Tests.SqlQueryLifecycleTest` 直接回归 | `net8.0` | 83 passed, 0 failed, 0 skipped, 313 ms |
| `Bing.Data.Sql.Analyzers.Tests` | `net8.0` | 31 passed, 0 failed, 0 skipped, 4 s |
| `Bing.Dapper.Core.Tests` | `net6.0` | 134 passed, 0 failed, 0 skipped, 440 ms |
| `Bing.Dapper.Core.Tests` | `net8.0` | 134 passed, 0 failed, 0 skipped, 323 ms |
| SQL Server Startup 环境变量隔离筛选 | `net6.0` | 3 passed, 0 failed, 0 skipped, 52 ms |
| SQL Server Startup 环境变量隔离筛选 | `net8.0` | 3 passed, 0 failed, 0 skipped, 50 ms |
| `Invoke-ProviderIntegrationTests.ps1 -SelfTest` | N/A | passed |

## 执行命令

```powershell
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net6.0 --no-build --no-restore --nologo -v quiet
dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -c Release -f net8.0 --no-build --no-restore --nologo -v quiet
dotnet test .\framework\tests\Bing.Data.Sql.Analyzers.Tests\Bing.Data.Sql.Analyzers.Tests.csproj -c Release --no-restore --nologo -v quiet
dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -f net6.0 --no-restore --nologo -v quiet
dotnet test .\framework\tests\Bing.Dapper.Core.Tests\Bing.Dapper.Core.Tests.csproj -c Release -f net8.0 --no-restore --nologo -v quiet
.\eng\ci\Invoke-ProviderIntegrationTests.ps1 -SelfTest
```

构建期间存在既有 PublicApiAnalyzers `RS0026/RS0027` warning；没有新增 build error。
