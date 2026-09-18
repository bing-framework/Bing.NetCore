# Bing.NetCore

[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://mit-license.org/)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/bing-framework/Bing.NetCore)

**English** ｜ [简体中文](README.md)

Bing.NetCore is an application framework built on .NET. It targets small teams that need to ship
business applications quickly: a set of common utilities, layered architecture base classes,
third‑party component wrappers, and integration modules for common infrastructure.

> **Note on documentation language**: the framework's reference documentation is currently written
> in Simplified Chinese. This file is the English entry point; the full documentation set lives in
> [`docs/`](docs/README.md), and an English index of that set is at
> [docs/README.en.md](docs/README.en.md).

## Key characteristics

| | |
| --- | --- |
| **Target frameworks** | Class libraries: `netstandard2.0` · Web / application hosts: `net6.0` |
| **Build SDK** | Pinned by [`global.json`](global.json) to **.NET SDK 8.0.424** |
| **Modularity** | Explicit module graph via `IBingModule` + `[DependsOnModule]`, two‑phase bootstrap |
| **Dependency injection** | Convention‑based auto‑registration (marker interfaces + `[Dependency]`), built on MSDI |
| **Data access** | Three coexisting options — EF Core (5 providers), FreeSQL (MySql), Dapper (`Bing.Data.Sql` engine) |
| **DDD building blocks** | `IStore` (not `IRepository`), aggregate roots, unit of work, auditing, soft delete, optimistic concurrency |
| **Cross‑cutting** | Unified API response, exception pipeline, multi‑tenancy, local + distributed event bus (CAP), caching, locks, email, templating, localization |
| **Packages** | **67 packable projects** under `framework/src` and `components/src` |

## Quick start

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

Registering a data access unit of work (EF Core + MySQL) — this is the shape used by the reference
implementation in `modules/admin`:

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

Details that commonly trip people up:

- **`AddMySqlUnitOfWork<TService, TImplementation>` takes two type parameters** and a connection
  string (or a `DbConnection`) — it is *not* a `DbContext`-options builder. The interface type comes
  first, the `UnitOfWorkBase`-derived implementation second.
- **Non‑Web hosts** call `serviceProvider.UseBing()` instead of `app.UseBing()`. Forgetting this
  means modules are registered but never started.
- **Module execution order is `Level` → `Order` → `FullName`**, *not* a topological sort of
  `[DependsOnModule]`. Dependencies are included recursively, but ordering is not derived from them —
  set `Order` when two modules at the same level must sequence.
- **There is no `AddAop`.** AOP is enabled on the service collection
  (`services.EnableAop(config => ...)`) or via AspectCore's `.UseServiceContext()` on the host builder.
- **The PostgreSQL entry point is named `AddPgSqlUnitOfWork`** (not `AddPostgreSqlUnitOfWork`), even
  though the project directory is `Bing.EntityFrameworkCore.PostgreSql`.

## Documentation

Full index: **[docs/README.md](docs/README.md)** (Chinese) · **[docs/README.en.md](docs/README.en.md)** (English map)

| Document | What it covers |
| --- | --- |
| [使用文档 (Usage)](docs/getting-started/使用文档.md) | Install, quick start, core concepts, per‑feature examples |
| [架构文档 (Architecture)](docs/architecture/架构文档.md) | Layers, real project reference graph, module inventory, full registration entry list |
| [设计思路文档 (Design rationale)](docs/architecture/设计思路文档.md) | Design goals, key decisions and their trade‑offs |
| [子系统深挖 (Subsystem deep dive)](docs/architecture/子系统深挖.md) | Code‑level analysis of CAP event bus, FreeSQL, `Bing.Data.Sql` |
| [能力矩阵 (Capability matrix)](docs/getting-started/能力矩阵.md) | Which ORM to pick — EF Core / FreeSQL / Dapper compared across 18 dimensions |
| [术语表 (Glossary)](docs/getting-started/术语表.md) | Framework‑specific vocabulary and easily confused terms |
| [最佳实践 (Best practices)](docs/guides/最佳实践.md) | Do / Don't pairs and antipatterns |
| [安全指南 (Security guide)](docs/operations/安全指南.md) | Credentials, JWT, SQL injection, log redaction, tenant isolation |
| [性能指南 (Performance guide)](docs/operations/性能指南.md) | Benchmarked guidance and known hot‑path traps |
| [测试指南 (Testing guide)](docs/operations/测试指南.md) | How to write and run tests, Public API gates, CI scripts |
| [abp-migration](docs/getting-started/abp-migration.md) | Type‑by‑type mapping for teams coming from ABP Framework |
| [架构决策记录 (ADRs)](docs/architecture/adr/README.md) | 10 decision records, each with rejected alternatives |
| [recipes](docs/guides/recipes/README.md) | End‑to‑end scenario code (CRUD, CAP events, multi‑tenancy, RBAC, payments, bulk/reporting) |
| [Issue tracker / FAQ](docs/getting-started/FAQ与排错.md) | The most common pitfalls, with source‑verified explanations |

Interactive helpers (open locally in a browser, zero dependencies):

- [选型向导 (Selection wizard)](docs/选型向导.html) — answer a few questions, get a package list and startup code
- [Cheat sheet](docs/cheatsheet.html) — one printable page from entity to CRUD API

## Packages

The full table with NuGet version / download badges for all **67 packages** is maintained in the
Chinese [README](README.md#nuget-packages). Core groups:

| Group | Packages |
| --- | --- |
| **Core & DI** | `Bing.Core` · `Bing.Uow` · `Bing.ExceptionHandling` · `Bing.ObjectMapping` · `Bing.AutoMapper` |
| **Domain (DDD)** | `Bing.Ddd.Domain` · `Bing.Ddd.Application` (+ `.Contracts`) · `Bing.Auditing` (+ `.Contracts`) |
| **ASP.NET Core** | `Bing.AspNetCore` (+ `.Abstractions`, `.Mvc`, `.Mvc.Contracts`, `.Mvc.UI`, `.Serilog`) · `Bing.AspNetCore.Authentication.JwtBearer` · `Bing.AspNetCore.MultiTenancy` |
| **Data access** | `Bing.Data` · `Bing.Data.Sql` (+ `.Analyzers`) · `Bing.Dapper.*` (5 providers) · `Bing.EntityFrameworkCore.*` (5 providers) · `Bing.FreeSQL` (+ `.MySql`) |
| **Cross‑cutting** | `Bing.Caching.*` · `Bing.Events` · `Bing.Locks.CSRedis` · `Bing.Localization` · `Bing.MultiTenancy` · `Bing.Emailing` · `Bing.MailKit` · `Bing.TextTemplating` (+ `.Scriban`) · `Bing.Logging` (+ `.Serilog`, `.Sinks.Exceptionless`) |
| **Security** | `Bing.Security` · `Bing.Permissions` · `Bing.Validation` (+ `.Abstractions`) |
| **Business** | `Bing.Biz` · `Bing.Biz.Payments` · `Bing.Biz.OAuthLogin` |

Every package directory contains its own `README.md`, which is embedded into the `.nupkg`
(via `PackageReadmeFile`) and rendered on its nuget.org page.

## Build and test

```bash
dotnet build Bing.All.sln
dotnet test framework/tests/<TestProject>/<TestProject>.csproj
```

- Test stack: **xUnit + Shouldly + Moq + Coverlet**.
- SQLite integration tests run by default; external databases (SqlServer / MySql / PostgreSQL /
  Oracle) are gated behind `RUN_INTEGRATION_TESTS` or provider‑specific environment variables.
  **Never point them at a production database.**
- Docs consistency is checked by `python eng/ci/check-docs.py` (also wired into CI via
  `.github/workflows/docs-lint.yml`).

See [CONTRIBUTING.md](CONTRIBUTING.md) (Chinese) and [AGENTS.md](AGENTS.md) for contribution rules —
note that the repository mandates **UTF‑8 for all text files** and Chinese XML doc comments.

## Reference implementation and samples

- [`modules/admin/`](modules/admin/README.md) — full layered business reference (domain → store → application service → Web API). Requires MySQL / Redis / RabbitMQ.
- [`samples/`](samples/README.md) — three minimal projects: minimal Web API host, console + Hangfire background jobs, and a non‑Web WinForms host.

## Contributing

Issues and pull requests are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md).

> **API compatibility**: the project states that API compatibility is *not* guaranteed across
> releases for cost reasons. Always check the [release notes](docs/ReleaseNotes.md) and the
> [migration guide](docs/migrations/README.md) before upgrading.

## Disclaimer

The code has been reviewed and is used in production by its authors, but unknown defects may
remain. The Bing team accepts no liability for production losses. See the Chinese
[README](README.md) for the full disclaimer.

## License

**MIT** — see [LICENSE](LICENSE).

## Links

- Source: <https://github.com/bing-framework/Bing.NetCore>
- Documentation site: built and deployed by `.github/workflows/docfx.yml` (GitHub Pages)
