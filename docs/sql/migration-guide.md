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
