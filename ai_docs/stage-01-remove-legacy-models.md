# 阶段一：清理旧模型残留

## 完成项

- 确认 `framework/src` 和 `framework/tests` 的有效源码已不存在 `DatabaseRole`、`DatabaseDescriptor`、`IDatabaseDescriptorResolver`、`DefaultDatabaseDescriptorResolver`、`SqlMetadataOptions.Databases` 与 `MappingVersion`。
- 更新 `docs/sqlquery-usage.md`：删除旧数据源描述器、角色配置、`Databases` 兼容说明、调用方传入 `DatabaseType`/角色的 Factory 示例，以及旧诊断参数字段的兼容说明。
- 文档的数据源示例仅使用 `SqlDataSourceOptions.DataSources` 与 `SqlDataSourceDescriptor`。

## 残留检查

在排除 `bin`、`obj` 与生成 XML 后，对 `framework/src`、`framework/tests`、`docs` 执行以下关键字检查无输出：

- `DatabaseRole`
- `DatabaseDescriptor`
- `IDatabaseDescriptorResolver`
- `DefaultDatabaseDescriptorResolver`
- `GetDatabaseDescriptorKey`
- `MappingVersion`
- `.Databases`

## 验证

- `dotnet build`：通过，113 个既有警告。
- `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -nologo -v minimal`：910 passed。
- MySql、SqlServer、PostgreSql、Sqlite、Oracle Provider 基线单元测试均通过。

## 风险

- 生成目录中的历史 XML 仍可能包含旧 API 文档，但不参与源代码编译或残留验收。
- EF Core、事务作用域、统一诊断和多 Provider DI 注册仍在后续阶段实施。

## 2026-07-10 复核

对 `framework/src`、`framework/tests` 与 `docs` 执行旧模型残留检查，未发现以下符号：

- `DatabaseRole`
- `DatabaseDescriptor`
- `IDatabaseDescriptorResolver`
- `DefaultDatabaseDescriptorResolver`
- `GetDatabaseDescriptorKey`
- `MappingVersion`
- `SqlMetadataOptions.Databases`

阶段一现有代码已符合最终模型，不新增兼容代码或无意义删除操作。
