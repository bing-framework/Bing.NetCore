# Bing.Uow
[![NuGet](https://img.shields.io/nuget/v/Bing.Uow.svg)](https://www.nuget.org/packages/Bing.Uow/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Bing.Uow.svg)](https://www.nuget.org/packages/Bing.Uow/) ![TFM](https://img.shields.io/badge/TFM-netstandard2.0-blue.svg)

> 工作单元契约：`IUnitOfWork` / `IUnitOfWorkManager` 与抽象基类。
>
> **目标框架**：`netstandard2.0` ｜ **分层**：L1 地基与核心 ｜ **当前版本**：7.0.0 ｜ **源文件**：3
>
> **上游 Bing 包**：`Bing.Core`

---

## 这是什么

定义"一次业务操作的提交边界"。具体的数据访问实现（EF Core / FreeSQL / Dapper）各自提供 `IUnitOfWork` 的实现，业务代码只依赖本包的抽象。

## 安装

```bash
dotnet add package Bing.Uow
```

## 快速上手

```csharp
public class OrderAppService
{
    private readonly IUnitOfWorkManager _uowManager;
    public OrderAppService(IUnitOfWorkManager uowManager) => _uowManager = uowManager;

    public async Task CreateAsync()
    {
        using var uow = _uowManager.Begin();
        // ... 多个仓储写入
        await uow.CommitAsync();   // 提交边界
    }
}
```

## 关键类型

| 类型 | 命名空间 | 说明 |
| --- | --- | --- |
| `IUnitOfWork` | `Bing.Uow` | `Commit()` / `CommitAsync()` 提交边界 |
| `IUnitOfWorkManager` | `Bing.Uow` | 创建与管理工作单元 |

## 与其它包的关系

**本包依赖**：`Bing.Core`

## 注意事项

真正可用的实现由 `Bing.EntityFrameworkCore*` / `Bing.FreeSQL*` 提供，本包只有契约。

## 更多文档

- [使用文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/使用文档.md) —— 安装、快速开始、注册入口速查（§3.3）
- [架构文档](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/architecture/架构文档.md) —— 分层、模块清单（§11）、注册入口完整清单（§12）
- [FAQ 与排错](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/getting-started/FAQ与排错.md)
- [迁移指南](https://github.com/bing-framework/Bing.NetCore/blob/master/docs/migrations/README.md) —— 跨版本升级的已知破坏性变更
- NuGet 包页面：https://www.nuget.org/packages/Bing.Uow

## 许可证

MIT —— 详见仓库根目录 [LICENSE](https://github.com/bing-framework/Bing.NetCore/blob/master/LICENSE)。
