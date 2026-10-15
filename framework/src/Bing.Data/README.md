# Bing.Data
[![NuGet](https://img.shields.io/nuget/v/Bing.Data.svg)](https://www.nuget.org/packages/Bing.Data/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Data.svg)](https://www.nuget.org/packages/Bing.Data/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 查询对象、分页与条件封装：`Query<TEntity>` / `Pager` / `ICondition`。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：59
>
> **上游 Bing 包**：`Bing.Core`
>
> **三方依赖**：`System.Linq.Dynamic.Core`

---

## 这是什么

提供 ORM 无关的**查询参数模型**：把"前端传来的查询条件"变成对象，交给仓储或应用服务消费。包含 `Query<TEntity>` / `IQuery`、`Pager` / `PagerList<T>`、以及各类 `ICondition` 实现。

库存一致性基础设施位于 `Bing.Data.Inventory`：`DbInventoryStore` 在调用方事务内使用余额条件更新，并以租户和请求 ID 的唯一键记录幂等结果。`DbInventoryOrderStore` 管理整单首次结果、多 SKU 不足回滚和只读恢复查询，默认按作用域注册。它们支持 MySQL/SQLite，不创建连接、不提交调用方事务；两套 Admin 的显式业务入口复用真实工作单元提交库存、关键审计和 CAP 出箱。跨进程竞争、整单死锁恢复及 Unknown 故障注入仍需验收，见[整单库存指南](../../../docs/guides/inventory-orders.md)。

有序事件收件箱的保留清理通过可选 `IOrderedEventStoreRetention` 和 `OrderedEventCoordinator.DeleteProcessedBeforeAsync` 显式执行，也可以通过默认关闭的 `AddBingOrderedEventRetentionScheduler` 按批次调度。内置 MySQL/SQLite 存储会在同一事务先把已处理记录写入 `bing_event_inbox_archive`，再删除收件箱记录；历史表继续保留事件 ID 的重复校验，待处理和失败记录始终保留。历史表需要由调用方在明确重放窗口后，通过可选 `IOrderedEventStoreArchiveRetention` 和 `DeleteArchivedBeforeAsync` 单独按批次清理，默认调度器不会调用该入口。框架不默认启动后台清理器，也不替应用决定历史表的重放期限、归档位置、游标、审计和业务幂等保留策略，部署方应先执行历史表迁移并确认这些依据仍可恢复后再启用调度。

`DbOrderedEventStore` 可通过 `maxPendingPerConsumer` 为逻辑消费者设置跨业务流的待处理总量上限。启用该参数前，部署方必须执行 `deploy/migrations/ordered-events/003-consumer-capacity.mysql.sql` 或 SQLite 对应迁移；容量行与收件箱在同一数据库事务中锁定并重新统计，默认不启用，不能把它当作跨数据库或 Redis 全局配额。容量表迁移与收件箱、归档表一样不会由应用启动自动执行。

## 库存身份与迁移

MySQL 首次建表使用 `TenantId`、`WarehouseId`、`RequestId` 的 `VARBINARY(512)` 和 `Sku` 的 `VARBINARY(1024)`，显式指定 `ENGINE=InnoDB` 与 `utf8mb4`；SQLite 使用 `TEXT COLLATE BINARY`。`DbInventoryStore` 对 MySQL 身份参数严格按 UTF-8 字节绑定，并从 `byte[]` 无损解码；`InventoryDeductionRequest` 保留大小写、尾空格和组合字符原值，继续使用 UTF-16 长度上限（租户、仓库、请求 128，SKU 256），并拒绝无效代理字符。

已有 MySQL `bing_inventory_stock` 和 `bing_inventory_request` 表不会被服务注册自动迁移。请先备份并核对存量身份，暂停所有旧版本和新版本的写入、消费及后台任务，再人工执行[精确身份迁移脚本](../../../deploy/migrations/inventory/002-exact-identities.mysql.sql)。脚本先执行 `CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_bin` 并切换到 InnoDB，再改为 `VARBINARY`；MySQL DDL 非事务化，不承诺一键回滚。迁移完成且所有实例升级前必须保持维护状态，不能新旧 schema 混跑；历史上已被旧排序规则合并的身份无法由迁移恢复，应先进行业务核对。该迁移不替代双进程竞争、Unknown 提交结果或整单死锁恢复验收。

## 安装

```bash
dotnet add package Bing.Data
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `Query<TEntity>` / `IQuery` | `Bing.Data.Queries` | 查询对象基类 |
| `Pager` / `PagerList<T>` | `Bing.Data` | 分页参数与分页结果 |
| `ICondition` | `Bing.Data` | 查询条件抽象 |
| `IInventoryStore` / `DbInventoryStore` | `Bing.Data.Inventory` | 同事务条件扣减和请求幂等记录 |
| `IInventoryOrderStore` / `DbInventoryOrderStore` | `Bing.Data.Inventory` | 整单首次结果、回滚与只读查询 |
| `InventoryOrderRequest` / `InventoryOrderLine` | `Bing.Data.Inventory` | 同仓库的不可变正数扣减请求 |

## 与其它包的关系

**本包依赖**：`Bing.Core`

**依赖本包**：`Bing.Events`、`Bing.MultiTenancy.Abstractions`

**三方 NuGet**：`System.Linq.Dynamic.Core`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Data

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
