# 阶段 0：基线验证

## 执行结果

- `dotnet build`：通过，113 个警告。
- `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -nologo -v minimal`：通过，912 passed。
- `dotnet test .\framework\tests\Bing.Dapper.MySql.Tests\Bing.Dapper.MySql.Tests.csproj -nologo -v minimal`：通过，156 passed。
- `dotnet test .\framework\tests\Bing.Dapper.SqlServer.Tests\Bing.Dapper.SqlServer.Tests.csproj -nologo -v minimal`：通过，166 passed。
- `dotnet test .\framework\tests\Bing.Dapper.PostgreSql.Tests\Bing.Dapper.PostgreSql.Tests.csproj -nologo -v minimal`：通过，124 passed。
- `dotnet test .\framework\tests\Bing.Dapper.Sqlite.Tests\Bing.Dapper.Sqlite.Tests.csproj -nologo -v minimal`：通过，4 passed。
- `dotnet test .\framework\tests\Bing.Dapper.Oracle.Tests\Bing.Dapper.Oracle.Tests.csproj -nologo -v minimal`：通过，106 passed。

## 既有警告

- `NETSDK1138`：多个 `net6.0` 目标框架已不受支持。
- `NU1902` / `NU1903` / `NU1904`：既有第三方包漏洞告警，涉及 `Scriban`、`AutoMapper`、`MailKit`、`MessagePack`。
- 若干既有 C# 警告：可空引用上下文、隐藏继承成员、过时 API、分析器规则等。

## 阶段结论

当前工作区在修改前可构建，目标 SQL/Dapper 单元测试全部通过。下一阶段开始删除本轮新增但不合理的兼容模型。

## 2026-07-10 复核

- `Bing.Data.Sql.Tests`：918 passed。
- `Bing.Dapper.MySql.Tests`：156 passed。存在 14 个既有 `CS8632` nullable 上下文警告。
- `Bing.Dapper.SqlServer.Tests`：170 passed。
- `Bing.Dapper.PostgreSql.Tests`：124 passed。
- `Bing.Dapper.Sqlite.Tests`：4 passed。存在 1 个既有 `NETSDK1206` SQLite RID 警告。
- `Bing.Dapper.Oracle.Tests`：106 passed。

本次六个指定单元测试共 1478 项通过、0 项失败。构建与测试未修改源代码。
