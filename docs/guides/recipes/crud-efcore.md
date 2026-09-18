# 配方 1：EF Core 端到端 CRUD

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

> 目标：一条从**实体 → 仓储 → 应用服务 → 控制器 → 数据库迁移**的完整链路，能跑、能提交、能返回统一响应。
>
> 适用：新业务系统，需要迁移与完整领域能力。若你只要写 SQL 报表，请改看 [配方 6](bulk-and-report.md)。

## 1. 装包

```bash
dotnet add package Bing.AspNetCore.Mvc
dotnet add package Bing.Ddd.Domain
dotnet add package Bing.Ddd.Application
dotnet add package Bing.Ddd.Application.Contracts
dotnet add package Bing.EntityFrameworkCore
dotnet add package Bing.EntityFrameworkCore.MySql     # 换成你的库
dotnet add package Bing.Aop.AspectCore                # 要用 [UnitOfWork] 就装
dotnet add package Bing.Uow
```

## 2. 实体

```csharp
using Bing.Domain.Entities;
using Bing.Auditing;

// AggregateRoot<TEntity,TKey> 已自带 IVersion（乐观锁 Version 属性，byte[]）
public class Order : AggregateRoot<Order, Guid>, ISoftDelete, IAuditedObject<Guid>
{
    public string OrderNo { get; set; }
    public decimal Amount { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }
}
```

> 需要删除审计再追加 `IDeletionAuditedObject<Guid>`。软删除与审计字段由 `UnitOfWorkBase` 在 `SaveChanges` 时自动填充/过滤。

## 3. 工作单元

```csharp
using Bing.Datas.EntityFramework.Core;
using Microsoft.EntityFrameworkCore;

public interface IAppUnitOfWork : IUnitOfWork { }

public class AppUnitOfWork : UnitOfWorkBase, IAppUnitOfWork
{
    public AppUnitOfWork(DbContextOptions<AppUnitOfWork> options,
                         IServiceProvider serviceProvider)
        : base(options, serviceProvider) { }

    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);                  // ⚠ 别漏：全局过滤器与映射靠它
        mb.Entity<Order>(b =>
        {
            b.ToTable("orders");
            b.HasKey(x => x.Id);
            b.Property(x => x.OrderNo).HasMaxLength(64).IsRequired();
        });
    }
}
```

> 构造函数签名请以你所用 Provider 的源码为准；`UnitOfWorkBase` 同时实现 `DbContext` 与 `IUnitOfWork`。

## 4. 仓储

```csharp
using Bing.Data;
using Bing.Datas.EntityFramework.Core;

public interface IOrderStore : IStore<Order, Guid>
{
    Task<Order> GetByNoAsync(string no);
}

public class OrderStore : StoreBase<Order, Guid>, IOrderStore
{
    public OrderStore(IUnitOfWorkManager uowManager) : base(uowManager) { }

    public Task<Order> GetByNoAsync(string no)
        => FindAsync(x => x.OrderNo == no);
}
```

> 约定式 DI 会自动注册（仓储基类实现了 `IScopedDependency`），**不用手写 `AddScoped`**。
> 只读路径可以只注入 `IQueryStore<Order, Guid>`（`IStore` 的父接口）。

## 5. 应用服务

```csharp
using Bing.Application.Services;

public class OrderAppService : AppServiceBase, IOrderAppService
{
    private readonly IOrderStore _store;
    public OrderAppService(IOrderStore store) => _store = store;

    [UnitOfWork]                       // ⚠ 依赖 AOP，见注意事项
    public async Task<Guid> CreateAsync(OrderCreateDto dto)
    {
        var entity = dto.MapTo<Order>();          // 需装 Bing.AutoMapper
        await _store.AddAsync(entity);
        return entity.Id;                          // 切面在方法成功后提交
    }

    public async Task<OrderDto> GetAsync(Guid id)
        => (await _store.FindByIdAsync(id)).MapTo<OrderDto>();

    // 显式提交也可以，不必依赖 AOP
    public async Task UpdateAmountAsync(Guid id, decimal amount)
    {
        var entity = await _store.FindByIdAsync(id);
        entity.Amount = amount;
        await _store.UpdateAsync(entity);
        await UnitOfWorkManager.CommitAsync();
    }
}
```

> 需要一整套增删改查时，直接继承 `CrudAppServiceBase<TEntity, TDto, TQueryParameter, TKey>`（它已内置 `[UnitOfWork]`）。

## 6. 控制器

```csharp
using Bing.AspNetCore.Mvc;

[ApiController, Route("api/orders")]
public class OrderController : ApiControllerBase
{
    private readonly IOrderAppService _app;
    public OrderController(IOrderAppService app) => _app = app;

    [HttpPost]
    public async Task<Guid> Create([FromBody] OrderCreateDto dto)
        => await _app.CreateAsync(dto);

    [HttpGet("{id:guid}")]
    public async Task<OrderDto> Get(Guid id) => await _app.GetAsync(id);
}
```

返回值会被全局结果过滤器自动包成：

```json
{ "code": "0", "message": "操作成功", "data": { }, "operationTime": "2026-09-18 15:00:00" }
```

## 7. 模块装配

```csharp
using Bing.Datas.EntityFramework.MySql;   // ⚠ 命名空间不是 Bing.EntityFrameworkCore.MySql

[ModuleLevel(ModuleLevel.Application)]
public class AppModule : BingModule
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMySqlUnitOfWork<IAppUnitOfWork, AppUnitOfWork>(
            config  => { /* 连接与行为配置 */ },
            builder => { /* DbContextOptionsBuilder */ });

        services.EnableAop(o => o.ThrowAspectException = true);   // [UnitOfWork] 必需
    }
}
```

## 8. 数据库迁移

EF Core 支是**唯一提供迁移的一支**，通过 `MigrationService.Migrate(...)` 调用 `dotnet ef database update`；种子数据用 `MigrationModuleBase<TDbContext>`。

```csharp
public class MigrationModule : MigrationModuleBase<AppUnitOfWork>
{
    // 迁移 + 种子数据初始化
}
```

> 想省事也可以直接用 EF 原生命令：`dotnet ef migrations add Init` → `dotnet ef database update`。

## 注意事项

1. 🔴 **`[UnitOfWork]` 依赖 AOP**：没调 `EnableAop(...)` 时它**静默失效**——方法正常返回但**数据不落库**。入口是 `EnableAop`，**没有 `AddAop`**。
2. 🔴 **`OnModelCreating` 里必须 `base.OnModelCreating(mb)`**：软删除全局过滤器在那里应用，漏了就会导致已删除数据被查出来。
3. **PostgreSQL 入口是 `AddPgSqlUnitOfWork`**，命名空间 `Bing.Datas.EntityFramework.PgSql`（工程目录却叫 `PostgreSql`）。
4. **没有批量写入扩展**：EF 支无 `BulkInsert` / `ExecuteUpdate`。上万条请走 [配方 6](bulk-and-report.md)。
5. **乐观锁冲突**会抛 `ConcurrencyException`（由 `DbUpdateConcurrencyException` 转换），业务侧要处理重试或提示。
6. **多租户不会自动过滤**，见 [配方 3](multi-tenancy.md)——这是另一个容易踩的坑。

## 相关

- [能力矩阵 §3](../../getting-started/能力矩阵.md) · [最佳实践 §4](../最佳实践.md) · [包索引](../../getting-started/包索引.md)
