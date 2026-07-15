# 阶段三：映射和缓存键收敛

## 完成项

- 确认实体映射配置、元数据和缓存键只使用实体类型、`DbKey`、`MappingProfile`、Schema 与表路由键，不依赖数据库角色或映射版本。
- 移除 `DefaultEntityMappingResolver` 在缺失上下文时自动构造 SQL Server 数据源的逻辑。
- 未绑定数据源时，映射解析仅执行通用 `DbType` 推断；Provider 类型转换器只在 `DatabaseContext.DataSource.DatabaseType` 已确定时参与。
- 新增不同 `MappingProfile` 的映射缓存隔离测试。

## 验证

- `dotnet build .\framework\src\Bing.Data.Sql\Bing.Data.Sql.csproj -nologo -v minimal`：通过。
- `dotnet test .\framework\tests\Bing.Data.Sql.Tests\Bing.Data.Sql.Tests.csproj -nologo -v minimal`：918 passed。

## 风险

- Factory 必须在创建 Query/Executor 时始终绑定数据源上下文，避免无 Provider 上下文进入执行路径；此项在阶段四验证。

## 2026-07-10 复核

- `EntityMappingOptions`、`EntityMappingMetadata` 和 `EntityMappingCacheKey` 仅包含最终映射维度。
- `DefaultEntityMappingResolver` 在缺少上下文时仅使用空通用上下文，不再注入历史 `Default` key 或 SQL Server Provider。
- `SqlParam.DatabaseType` 仅从 `DatabaseContext.DataSource.DatabaseType` 写入。
- 现有 SQL 核心 920 项测试覆盖跨数据源、跨 MappingProfile 的表名、列名与缓存隔离。
