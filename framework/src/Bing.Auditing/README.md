# Bing.Auditing
[![NuGet](https://img.shields.io/nuget/v/Bing.Auditing.svg)](https://www.nuget.org/packages/Bing.Auditing/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Auditing.svg)](https://www.nuget.org/packages/Bing.Auditing/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 审计：审计实体接口、`IAuditingManager` / `IAuditingStore` 与日志审计实现。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：21
>
> **上游 Bing 包**：无（本包是叶子/基础包）

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
| `IAuditingManager` | `Bing.Auditing` | `BeginScope()` + `Current` 采集审计 |
| `AuditLogContributor` | `Bing.Auditing` | 审计信息扩展点 |
| `IAuditingStore` | `Bing.Auditing` | 审计存储抽象（默认输出到日志） |

## 与其它包的关系

**依赖本包**：`Bing.EntityFrameworkCore`

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Auditing

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
