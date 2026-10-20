# Bing.Auditing
[![NuGet](https://img.shields.io/nuget/v/Bing.Auditing.svg)](https://www.nuget.org/packages/Bing.Auditing/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Auditing.svg)](https://www.nuget.org/packages/Bing.Auditing/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 审计：审计实体接口、`IAuditingManager` / `IAuditingStore` 与日志审计实现。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：21
>
> **上游 Bing 包**：`Bing.Data`、`Bing.Auditing.Contracts`、`Bing.Security`

---

## 这是什么

实体实现 `ICreationAuditedObject` / `IModificationAuditedObject` / `IFullAuditedObject` 即可获得审计字段；`IAuditingManager` 负责采集，`IAuditingStore` 负责落库（默认 `SimpleLogAuditingStore` 输出到 `ILogger`）。

## 安装

```bash
dotnet add package Bing.Auditing
```

## 快速上手

```csharp
// 实体侧
public class Order : AggregateRoot<Guid>, IFullAuditedObject
{
    public DateTime? CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }
}

// 需要自定义落库时替换 IAuditingStore
services.AddTransient<IAuditingStore, MyAuditingStore>();
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IAuditPropertySetter` | `Bing.Auditing.Contracts` | ORM 写入前填充创建、修改和删除审计字段；`Bing.Auditing` 保留旧程序集类型转发 |
| `IAuditingManager` | `Bing.Auditing` | `BeginScope()` + `Current` 采集审计 |
| `AuditLogContributor` | `Bing.Auditing` | 审计信息扩展点 |
| `IAuditingStore` | `Bing.Auditing` | 审计存储抽象（默认输出到日志） |
| `ICancellableAuditingStore` | `Bing.Auditing` | 可选的调用方取消保存能力 |
| `DbAuditingStore` / `ITransactionalAuditingStore` | `Bing.Auditing.Persistence` | 普通审计独立落库；关键审计复用调用方事务 |
| `IAuditLogRetentionStore` / `IAuditLogTenantRetentionStore` | `Bing.Auditing.Persistence` | 按 UTC 截止时间清理全局或指定租户的普通审计 |
| `IAuditLogArchiveStore` / `IAuditLogArchiveService` | `Bing.Auditing.Persistence` | 经授权、单事务、显式分批归档普通审计 |
| `IAuditLogArchiveRetentionStore` / `IAuditLogArchiveRetentionService` | `Bing.Auditing.Persistence` | 经独立授权、有界删除归档历史 |
| `AuditLogArchiveRetentionHostedService` | `Bing.Auditing.Persistence` | 默认关闭、可选协调租约的归档历史清理调度 |
| `IAuditLogQueryStore` | `Bing.Auditing.Persistence` | 按租户和有界条件读取审计元数据 |
| `IAuditLogRetentionService` / `IAuditLogRetentionAuthorizer` | `Bing.Auditing.Persistence` | 先授权再分批清理全局普通审计 |
| `IAuditLogTenantRetentionService` / `IAuditLogTenantRetentionAuthorizer` | `Bing.Auditing.Persistence` | 先授权再清理指定租户的普通审计 |
| `IAuditLogQueryService` / `IAuditLogQueryAuthorizer` | `Bing.Auditing.Persistence` | 先授权再读取审计元数据 |

数据库持久化通过 `AddBingDatabaseAuditing(database, connectionFactory, configure)` 显式启用，部署时建立 `AuditLogSchema` 表；已有表应按 `deploy/migrations/auditing/004-tenant-retention-index.*.sql` 增加租户范围清理索引，并按 `005-audit-log-archive.*.sql` 创建归档表。默认不保存参数或异常正文，实体属性值使用白名单。面向 HTTP、管理后台或运维任务时，应通过 `AddBingAuditLogRetention<TAuthorizer>()`、`AddBingAuditLogArchive<TAuthorizer>()`、`AddBingAuditLogArchiveRetention<TAuthorizer>()`、`AddBingAuditLogTenantRetention<TAuthorizer>()` 和 `AddBingAuditLogQuery<TAuthorizer>()` 注册高层授权入口；归档入口复用保留授权器但不会自动启动调度，归档历史清理使用独立授权器，也不会自动启动调度。调用方必须明确保留重放窗口、外部备份和归档表容量策略。低层存储契约仍保留用于基础设施适配，但不替代宿主的租户、角色和用途授权。租户清理按原始标识精确匹配，首尾空白和 Unicode 大小写不会被修剪或规范化。关键审计由 `IsCritical` 标记保护，旧表需先执行审计迁移。存在活动审计作用域时，EFCore 和 FreeSQL 工作单元会自动收集实体变更快照；两套 ORM 对实体元数据、属性读取或值格式化失败均采用尽力采集，只跳过无法读取的实体或属性，不误报已提交业务，宿主原有回调异常仍传播；FreeSQL 通知位于物理提交后。该快照不替代关键事务审计。两套 ORM 可用 `SaveTransactionalAuditAsync` 在业务事务内写入关键审计，失败时必须回滚。详见[数据库审计指南](../../../docs/guides/auditing-persistence.md)。

`EntityChangeInfo.ChangeType` 已从错误的自身类型修正为 `EntityChangeType` 枚举；使用该成员的旧编译消费者需重新编译。

## 与其它包的关系

**依赖本包**：`Bing.EntityFrameworkCore`、`Bing.FreeSQL`（操作审计适配，无 AspectCore 依赖）

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Auditing

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
