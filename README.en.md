# Bing.NetCore

[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://mit-license.org/)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/bing-framework/Bing.NetCore)

**中文译文** ｜ [中文主入口](README.md)

Bing.NetCore 是一个基于 .NET 构建的应用框架。它面向需要快速交付业务应用的小型团队，提供常用工具、分层架构基类、
第三方组件封装，以及常见基础设施的集成模块。

> **文档语言说明**：框架参考文档目前以简体中文编写。本文件提供中文入口；完整文档集位于
> [`docs/`](docs/README.md)，对应的文档导航见 [docs/README.en.md](docs/README.en.md)。

## 主要特性

| | |
| --- | --- |
| **目标框架** | 类库：`netstandard2.0` · Web / 应用宿主：`net6.0` |
| **构建 SDK** | [`global.json`](global.json) 固定为 **.NET SDK 8.0.424** |
| **模块化** | 通过 `IBingModule` + `[DependsOnModule]` 显式声明模块图，并采用两阶段启动 |
| **依赖注入** | 基于约定的自动注册（标记接口 + `[Dependency]`），底层使用 MSDI |
| **数据访问** | 三种并存选项：EF Core（5 个 Provider）、FreeSQL（MySql）、Dapper（`Bing.Data.Sql` 引擎） |
| **DDD 构建块** | `IStore`（不是 `IRepository`）、聚合根、工作单元、审计、软删除、乐观并发 |
| **横切能力** | 统一 API 响应、异常管道、多租户、本地 + 分布式事件总线（CAP）、缓存、锁、邮件、模板、本地化 |
| **包数量** | `framework/src` 与 `components/src` 下共有 **70 个可打包工程** |

## 快速开始

```bash
dotnet add package Bing.AspNetCore
```

```csharp
using Bing.AspNetCore;
using Bing.Core.Modularity;

// A module is the unit of assembly.
[DependsOnModule(typeof(AspNetCoreModule))]
public class MyAppModule : AspNetCoreBingModule      // plain BingModule for non-Web hosts
{
    public override ModuleLevel Level => ModuleLevel.Application;

    public override IServiceCollection AddServices(IServiceCollection services)
    {
        services.AddControllers();
        return services;
    }

    public override void UseModule(IApplicationBuilder app)
    {
        app.UseRouting();
    }
}

public class Startup
{
    public Startup(IConfiguration configuration) => Configuration = configuration;
    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddBing()                   // returns IBingBuilder
                .AddModule<MyAppModule>();   // [DependsOnModule] pulls in the rest of the graph
    }

    public void Configure(IApplicationBuilder app) => app.UseBing();
}
```

注册数据访问工作单元（EF Core + MySQL）。`modules/admin` 中的参考实现采用以下形式：

```csharp
public class EntityFrameworkCoreModule : BingModule
{
    public override ModuleLevel Level => ModuleLevel.Framework;
    public override int Order => 1;

    public override IServiceCollection AddServices(IServiceCollection services)
    {
        var connectionString = services.GetConfiguration().GetConnectionString("DefaultConnection");

        services.AddMySqlUnitOfWork<IAdminUnitOfWork, AdminUnitOfWork>(connectionString);
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", DatabaseType.MySql, connectionString);

        return services;
    }
}
```

常见易错点：

- **`AddMySqlUnitOfWork<TService, TImplementation>` 接收两个类型参数**和一个连接字符串（或 `DbConnection`），
  它*不是* `DbContext` 选项构建器。第一个类型是接口，第二个类型是派生自 `UnitOfWorkBase` 的实现。
- **非 Web 宿主**调用 `serviceProvider.UseBing()`，而不是 `app.UseBing()`。遗漏这一步会导致模块已注册但从未启动。
- **模块执行顺序为 `Level` → `Order` → `FullName`**，*不是* `[DependsOnModule]` 的拓扑排序。依赖会递归纳入，
  但顺序不会从依赖关系推导；同一级别的模块需要排序时，请设置 `Order`。
- **不存在 `AddAop`。** AOP 可在服务集合上启用（`services.EnableAop(config => ...)`），或通过宿主构建器上的
  AspectCore `.UseServiceContext()` 启用。
- **PostgreSQL 入口名称是 `AddPgSqlUnitOfWork`**（不是 `AddPostgreSqlUnitOfWork`），即使工程目录名为
  `Bing.EntityFrameworkCore.PostgreSql`。

## 文档

完整索引：**[docs/README.md](docs/README.md)** · **[docs/README.en.md](docs/README.en.md)**

| 文档 | 覆盖内容 |
| --- | --- |
| [使用文档](docs/getting-started/使用文档.md) | 安装、快速开始、核心概念和按功能组织的示例 |
| [架构文档](docs/architecture/架构文档.md) | 分层结构、真实项目参考依赖图、模块清单和完整注册入口列表 |
| [设计思路文档](docs/architecture/设计思路文档.md) | 设计目标、关键决策及其权衡 |
| [子系统深挖](docs/architecture/子系统深挖.md) | CAP 事件总线、FreeSQL、`Bing.Data.Sql` 的代码级分析 |
| [能力矩阵](docs/getting-started/能力矩阵.md) | 通过 18 个维度比较 EF Core、FreeSQL 与 Dapper，帮助选择 ORM |
| [术语表](docs/getting-started/术语表.md) | 框架专用词汇和容易混淆的术语 |
| [最佳实践](docs/guides/最佳实践.md) | 应做 / 不应做对照和反模式 |
| [安全指南](docs/operations/安全指南.md) | 凭据、JWT、SQL 注入、日志脱敏和租户隔离 |
| [性能指南](docs/operations/性能指南.md) | 基准测试结论和已知热点路径陷阱 |
| [测试指南](docs/operations/测试指南.md) | 如何编写和运行测试、公开 API 门禁及 CI 脚本 |
| [abp-migration](docs/getting-started/abp-migration.md) | 面向 ABP Framework 团队的逐类型映射 |
| [架构决策记录](docs/architecture/adr/README.md) | 10 份架构决策记录，每份都包含被否决的备选方案 |
| [recipes](docs/guides/recipes/README.md) | 端到端场景代码（CRUD、CAP 事件、多租户、RBAC、支付、批量处理 / 报表） |
| [问题追踪 / FAQ](docs/getting-started/FAQ与排错.md) | 最常见的问题及经过源码核对的解释 |

### 模块文档（12 篇）

| 模块 | 一句话简介 |
| --- | --- |
| [核心启动与依赖注入](docs/modules/core.md) | 模块发现、依赖加载、约定注册、AOP 与异常基础设施。 |
| [Web 与 MVC](docs/modules/web.md) | ASP.NET Core 启动、统一响应、异常处理与 Razor 扩展。 |
| [动态 API](docs/modules/dynamic-api.md) | 将 `IAppService` 约定公开为 HTTP API，并提供运行时代理与 WebApiClientCore 两种客户端。 |
| [领域与工作单元](docs/modules/domain.md) | DDD、应用服务、审计、验证、提交与对象映射。 |
| [数据访问](docs/modules/data-access.md) | EF Core、FreeSQL、Dapper、SQL Builder 与各数据库 Provider。 |
| [缓存](docs/modules/caching.md) | 统一缓存抽象、Redis 适配器、EasyCaching、命名空间、拓扑、分片和 Hybrid 协调。 |
| [事件与消息](docs/modules/events.md) | 进程内发布、CAP 消息及领域事件分发的边界。 |
| [安全与多租户](docs/modules/security.md) | JWT、授权、权限、安全服务及租户解析和切换。 |
| [日志与诊断](docs/modules/observability.md) | Bing 日志、Serilog、Exceptionless、SkyAPM 与 MiniProfiler。 |
| [外围集成](docs/modules/integrations.md) | 邮件、模板、本地化、锁、支付与 OAuth 的注册和限制。 |
| [工程与测试](docs/modules/development.md) | 分析器、生成器、构建、测试和文档校验工具。 |
| [参考宿主](docs/modules/reference-apps.md) | admin 分层参考实现及 WebAPI、Hangfire、WinForms 示例。 |

### 本次文档更新清单

逐文件的操作类型、内容摘要和验证边界见正式清单：[docs/operations/本次文档更新清单.md](docs/operations/本次文档更新清单.md)。

交互式辅助工具（本地浏览器打开，零依赖）：

- [选型向导](docs/选型向导.html) — 回答几个问题，获取包清单和启动代码
- [Cheat sheet](docs/cheatsheet.html) — 从实体到 CRUD API 的一页纸速查

## 包

全部 **70 个包**的 NuGet 版本 / 下载徽章完整表格维护在中文 [README](README.md#nuget-packages) 中。核心分组如下：

| 分组 | 包 |
| --- | --- |
| **核心与 DI** | `Bing.Core` · `Bing.Uow` · `Bing.ExceptionHandling` · `Bing.ObjectMapping` · `Bing.AutoMapper` |
| **领域（DDD）** | `Bing.Ddd.Domain` · `Bing.Ddd.Application`（+ `.Contracts`）· `Bing.Auditing`（+ `.Contracts`） |
| **ASP.NET Core** | `Bing.AspNetCore`（+ `.Abstractions`、`.Mvc`、`.Mvc.Contracts`、`.Mvc.UI`、`.Serilog`）· `Bing.AspNetCore.Authentication.JwtBearer` · `Bing.AspNetCore.MultiTenancy` |
| **动态 API** | `Bing.DynamicApi.Contracts` · `Bing.DynamicApi.Client` · `Bing.DynamicApi.WebApiClient` |
| **数据访问** | `Bing.Data` · `Bing.Data.Sql`（+ `.Analyzers`）· `Bing.Dapper.*`（5 个 Provider）· `Bing.EntityFrameworkCore.*`（5 个 Provider）· `Bing.FreeSQL`（+ `.MySql`） |
| **横切能力** | `Bing.Caching.*` · `Bing.Events` · `Bing.Locks.CSRedis` · `Bing.Localization` · `Bing.MultiTenancy` · `Bing.Emailing` · `Bing.MailKit` · `Bing.TextTemplating`（+ `.Scriban`）· `Bing.Logging`（+ `.Serilog`、`.Sinks.Exceptionless`） |
| **安全** | `Bing.Security` · `Bing.Permissions` · `Bing.Validation`（+ `.Abstractions`） |
| **业务** | `Bing.Biz` · `Bing.Biz.Payments` · `Bing.Biz.OAuthLogin` |

70 个包目录都包含自己的 `README.md`，它们通过 `PackageReadmeFile` 嵌入 `.nupkg`，并在 nuget.org 包页面中展示。

## 构建与测试

```bash
dotnet build Bing.All.sln
dotnet test framework/tests/<TestProject>/<TestProject>.csproj
```

- 测试技术栈：**xUnit + Shouldly + Moq + Coverlet**。
- SQLite 集成测试默认运行；外部数据库（SqlServer / MySql / PostgreSQL / Oracle）由
  `RUN_INTEGRATION_TESTS` 或 Provider 专用环境变量门控。**切勿指向生产数据库。**
- 文档一致性由 `python eng/ci/check-docs.py` 检查（同时通过 `.github/workflows/docs-lint.yml` 接入 CI）。

贡献规则见 [CONTRIBUTING.md](CONTRIBUTING.md) 和 [AGENTS.md](AGENTS.md)；请注意，仓库要求**所有文本文件使用 UTF-8**，
XML 文档注释使用中文。

## 参考实现与示例

- [`modules/admin/`](modules/admin/README.md) — 完整的分层业务参考实现（领域 → Store → 应用服务 → Web API）。需要 MySQL / Redis / RabbitMQ。
- [`samples/`](samples/README.md) — 三个最小项目：最小 Web API 宿主、控制台 + Hangfire 后台任务，以及非 Web WinForms 宿主。

## 贡献

欢迎提交 Issue 和 Pull Request，详见 [CONTRIBUTING.md](CONTRIBUTING.md)。

> **API 兼容性**：由于成本原因，项目声明不保证不同版本之间的 API 兼容性。升级前请务必查看
> [发行说明](docs/ReleaseNotes.md) 和 [迁移指南](docs/migrations/README.md)。

## 免责声明

代码已经过审查，并由作者用于生产环境，但仍可能存在未知缺陷。Bing 团队不对生产损失承担责任。
完整免责声明请参阅中文 [README](README.md)。

## 许可证

**MIT** — 详见 [LICENSE](LICENSE)。

## 链接

- 源码：<https://github.com/bing-framework/Bing.NetCore>
- 文档站点：由 `.github/workflows/docfx.yml` 构建并部署到 GitHub Pages
