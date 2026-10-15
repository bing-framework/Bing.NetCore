# 审计

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

## 功能说明

审计模块提供同步/异步审计作用域、默认日志存储、HTTP 请求采集、数据库持久化、查询、归档、保留清理和可选后台调度。EF Core 与 FreeSQL 均有事务内审计写入适配。

## 核心实现

- 管理器与注册：`framework/src/Bing.Auditing/Bing/Auditing/AuditingManager.cs`、`AuditingRegistrationExtensions.cs`
- 持久化：`framework/src/Bing.Auditing/Bing/Auditing/Persistence/`
- HTTP 采集：`framework/src/Bing.AspNetCore/Bing/AspNetCore/Auditing/`
- ORM 适配：`framework/src/Bing.EntityFrameworkCore/Auditing/`、`framework/src/Bing.FreeSQL/Bing/Auditing/`

## 用法

```csharp
services.AddBingHttpAuditing(options =>
{
    options.Enabled = true;
});

app.UseRouting();
app.UseBingHttpAuditing();
```

无 HTTP 场景可只调用 `services.AddBingAuditing()`，通过 `IAuditingManager` 创建作用域。数据库表、查询、归档和保留策略见[审计持久化指南](../guides/auditing-persistence.md)。

## 依赖与配置

- 默认 `SimpleLogAuditingStore` 写日志；持久化必须显式注册数据库存储。
- HTTP 中间件应放在 `UseRouting()` 之后，才能识别端点上的禁用标记。
- 保留、归档和归档清理使用独立授权器，后台调度默认不应替代业务授权。

## 限制与注意事项

- 审计不是跨存储原子事务；关键业务审计应使用对应 ORM 的事务适配。
- 默认不要记录密码、令牌和完整敏感请求体；宿主需配置白名单和长度限制。
- 归档不等于备份，删除前必须满足监管、重放和外部备份要求。
- 外部存储故障策略由宿主决定，不能把普通日志存储视为可靠审计落库。
