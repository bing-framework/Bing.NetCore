# 领域、应用服务与工作单元

## 功能说明

DDD 包提供实体、聚合根、值对象、仓储/存储契约和应用服务基类；`Bing.Uow` 定义提交契约；审计、验证和对象映射包为这条调用链提供横切能力。数据库落地实现见[数据访问模块](data-access.md)。

## 核心实现逻辑与关键文件

| 路径 | 职责 |
| --- | --- |
| `framework/src/Bing.Ddd.Domain/Bing/Domain/` | 实体、领域服务、仓储契约、领域事件分发器。 |
| `framework/src/Bing.Ddd.Application.Contracts/`、`framework/src/Bing.Ddd.Application/` | DTO、应用服务契约与查询、删除、CRUD 基类。 |
| `framework/src/Bing.Uow/Bing/Uow/` | `IUnitOfWork`、管理器与提交转发。 |
| `framework/src/Bing.Auditing/`、`framework/src/Bing.Auditing.Contracts/` | 审计接口与字段填充。 |
| `framework/src/Bing.Validation/`、`framework/src/Bing.Validation.Abstractions/` | 验证契约、DataAnnotations 及验证拦截标记。 |
| `framework/src/Bing.ObjectMapping/`、`framework/src/Bing.AutoMapper/` | 映射契约、AutoMapper 适配与注册。 |

应用服务调用仓储，具体 ORM 工作单元管理变更并提交；`UnitOfWorkAttribute` 的提交依赖 AOP 代理。领域事件自动分发已接入 EF Core 工作单元中的受跟踪聚合根，以及 FreeSQL `StoreBase` 的写入入口；注册分发器并由框架工作单元管理事务时，工作单元会分发并在保存成功后清理事件。直接调用 FreeSQL API、原始 SQL、自定义存储器或使用外部事务不在自动接入范围内。

## 对外接口与用法

已注入的工作单元提供以下提交接口：

```csharp
using Bing.Uow;

// unitOfWork 的类型为 IUnitOfWork；完成写入后显式提交。
await unitOfWork.CommitAsync(cancellationToken);
```

仓储接口使用 `Bing.Domain.Repositories` 命名空间。映射实现通过 `AddAutoMapper` 注册，单独安装 `Bing.ObjectMapping` 只得到契约。详细示例见[工作单元、仓储与应用服务](../guides/工作单元仓储与应用服务.md)。

## 依赖与配置项

ORM Provider 决定仓储与工作单元实现；AOP、当前用户与审计接口决定自动提交和字段填充的可用性。映射需提供业务 Profile，验证需明确规则与失败处理方式；这些组件没有共享的自动配置节。

## 已知限制与注意事项

- `IUnitOfWork` 提供 `Commit` / `CommitAsync`，不提供 `Begin`。
- 仓储写入不会普遍自动提交；使用已有内部提交的应用服务基类时应避免重复提交。
- 跨 ORM 的两个工作单元不构成统一事务。
- 领域事件自动登记与分发只覆盖上述 EF Core 跟踪实体和 FreeSQL `StoreBase` 写入路径；未注册分发器时不会派发，直接使用原生 API、原始 SQL、自定义存储器时需显式登记事件源。
- 领域对象全局验证规则：确认未实现；`DomainObjectBase` 的对应配置入口仍是占位。
- 源生成器是编译期能力，见[工程与分析器](development.md)，不能代替运行时注册。
