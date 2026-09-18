# RC 迁移指南

## Transaction Factory

事务工厂使用显式重载：

```csharp
factory.Begin();
factory.Begin(dataSourceKey);
factory.Begin(dataSourceKey, isolationLevel);

await factory.BeginAsync(cancellationToken);
await factory.BeginAsync(dataSourceKey, cancellationToken);
await factory.BeginAsync(dataSourceKey, isolationLevel, cancellationToken);
```

`dataSourceKey` 表示逻辑数据源标识。旧的 `dbKey` 可选参数形式不再作为新的公共 API 设计入口；调用方应按上表迁移。

## ToEntity

`ToEntity` / `ToEntityAsync` 保留为当前查询描述的唯一高层实体终结入口，其实际语义是零行返回默认值、单行返回实体、多行由底层 SingleOrDefault 语义处理。本文档不新增同义 `SingleOrDefault` 查询描述 API。

## Integration Settings

本机外部 Provider 使用未跟踪的 `integration.runsettings.local`，通过 `Invoke-ProviderIntegrationTests.ps1 -Settings <path>` 显式加载。受保护 CI 只使用 Provider 专属 secret；不能使用默认连接、生产库或 `RUN_INTEGRATION_TESTS` 全局 gate。

## 实体映射配置版本发布

`DefaultEntityMappingResolver` 在构造时深复制 `EntityMappingOptions` 及列配置。构造完成后直接修改原配置对象不会改变解析结果。

需要在运行时更新映射时，使用 `VersionedEntityMappingResolver.PublishMappings` 发布完整配置集合。发布成功后版本递增；新建 Query 或 Builder 捕获新版本，已创建对象及其 Clone 继续使用原快照，避免单次 SQL 构建混用新旧表名或列名。发布失败不会替换当前版本。

逻辑标识 `DbKey`、`MappingProfile` 和 `TableRouteKey` 采用去除首尾空白、大小写无关的匹配规则。Database、Schema 和 TableName 保留原始内容及大小写，并按精确值隔离缓存。

自定义解析器若需要参与固定版本生命周期，应实现 `IEntityMappingSnapshotProvider`。默认实现继续按 `DatabaseType` 隔离 Provider 行为；依赖额外上下文的自定义解析器必须自行保证缓存键完整性。
