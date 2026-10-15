# 数据访问与工作单元

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

## 功能说明

仓库同时提供 EF Core、FreeSQL 和 Bing.Data.Sql/Dapper 三条数据访问路径。EF Core 与 FreeSQL 实现仓储、工作单元、领域事件及审计接入；`Bing.Data` 还提供事务动作、消费幂等和库存条件扣减/订单投影等通用契约。

## 核心实现

- 通用事务：`framework/src/Bing.Data/Bing/Data/Transaction/`
- 库存：`framework/src/Bing.Data/Bing/Data/Inventory/`
- EF Core：`framework/src/Bing.EntityFrameworkCore/`
- FreeSQL：`framework/src/Bing.FreeSQL/`
- SQL/Dapper：`framework/src/Bing.Data.Sql/`
- 工作单元 AOP：`framework/src/Bing.Ddd.Application.AspectCore/`

## 用法

ORM 注册方式不同，先按[能力矩阵](../getting-started/能力矩阵.md)选择实现。库存能力显式注册存储和订单投影：

```csharp
services.AddBingInventoryStore(InventoryDatabase.MySql);
services.AddBingInventoryOrders(InventoryDatabase.MySql);
```

工作单元代理需同时启用 AspectCore 执行器，具体写法见[工作单元、仓储与应用服务](../guides/工作单元仓储与应用服务.md)。库存表结构和请求幂等见[库存订单指南](../guides/inventory-orders.md)。

## 依赖与配置

- EF Core 和 FreeSQL 使用各自 Provider；外部 MySQL/CAP 测试由环境变量门控。
- 库存键必须包含业务实际隔离维度，例如租户、仓库和 SKU。
- 幂等请求、库存条件更新、审计和出箱要在同一数据库事务中提交。

## 限制与注意事项

- FreeSQL 通用 `IQueryable` 异步扩展存在同步计算回退，不代表数据库异步 I/O。
- 三套 ORM 不能共享同一个物理事务；跨存储一致性需要消息和补偿。
- 多租户解析不会自动生成所有 ORM 的数据过滤条件。
- 外部 MySQL、CAP 和 RabbitMQ 的验证结果依赖专用测试环境，SQLite 不能替代分布式事务结论。

