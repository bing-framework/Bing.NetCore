# Recipes（可复用配方）

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

> **一句话定位**：比《使用文档》更完整的**端到端代码配方**——每个配方解决一个具体场景，给出「要装哪些包 + 完整代码 + 注意事项」。
>
> **与《使用文档》的分工**：使用文档讲「每个能力怎么用」（点），配方讲「几个能力串起来解决一个问题」（线）。

---

## 配方清单

| # | 配方 | 解决什么 | 难度 |
| --- | --- | --- | --- |
| 1 | [EF Core 端到端 CRUD](crud-efcore.md) | 从实体 → 仓储 → 应用服务 → 控制器 → 迁移，一个可跑的完整流程 | ⭐ |
| 2 | [CAP 分布式事件与 Outbox](cap-event-outbox.md) | 业务提交后可靠发消息 + 幂等消费 | ⭐⭐ |
| 3 | [多租户隔离](multi-tenancy.md) | 租户解析链 + **数据过滤**（⚠ 框架不自动过滤数据） | ⭐⭐⭐ |
| 4 | [授权与 RBAC](rbac-authorization.md) | 全局「默认拒绝」授权约定 + 权限模型落地 | ⭐⭐ |
| 5 | [支付接入与回调](payment.md) | 支付宝 / 微信下单与回调验签 | ⭐⭐ |
| 6 | [批量写入与跨库报表](bulk-and-report.md) | 大批量写、跨库 Join、存储过程 | ⭐⭐ |

---

## 使用前须知（请务必读）

1. **这些是「配方」不是「可编译工程」**。代码取自仓库真实模式（参考实现 `modules/admin`、样例 `samples/`、以及各包 README），但**没有作为独立工程加入 `Bing.All.sln` 进行编译验证**。跨版本升级后请先对照源码再复制。
2. **命名空间以源码为准**。框架有几处「包名 ≠ 运行时命名空间」的不一致（如 EF Core 运行时是 `Bing.Datas.EntityFramework.Core`，PostgreSQL 的命名空间是 `Bing.Datas.EntityFramework.PgSql`），配方中已标注。
3. **每个配方末尾都有「注意事项」**——那部分是踩坑高发区，别跳过。
4. 想找包看 [包索引](../../getting-started/包索引.md)；想选 ORM 看 [能力矩阵](../../getting-started/能力矩阵.md)；想知道对错看 [最佳实践](../最佳实践.md)。

---

## 通用前置步骤

```bash
# 1. 装包（以配方为准，这里只示例最核心的）
dotnet add package Bing.AspNetCore.Mvc
dotnet add package Bing.Ddd.Domain
dotnet add package Bing.Ddd.Application
```

```csharp
// 2. Program.cs —— 所有 Web 配方都一样
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddControllersAsServices();  // 属性注入需要
builder.Services.AddBing().AddModule<AppModule>();
var app = builder.Build();
app.UseBing();          // ⚠ 非 Web 宿主（控制台 / WinForm）用 serviceProvider.UseBing()
app.MapControllers();
app.Run();
```

> 🔴 **最常见的静默失败**：非 Web 宿主只调了 `AddBing()` 没调 `UseBing()`，模块初始化完全不执行但**不报错**。
