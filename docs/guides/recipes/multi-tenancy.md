# 配方 3：多租户隔离

> **适用版本**：框架 `7.0.0`（`version.props`）｜ 目标框架：类库 `netstandard2.0`、Web / 应用 `net6.0`｜ 内容核对：2026-09-18｜ 版本历史见 [发行说明](../../ReleaseNotes.md)

> 🚨 **先读这一句**：框架的租户解析链只回答「**当前租户是谁**」，**不过滤任何数据**。三支 ORM 都没有开箱即用的租户查询过滤。**漏配过滤 = 跨租户数据泄露**。
>
> 本配方分两段：① 解析租户（框架提供）→ ② 过滤数据（**你必须自己写**）。

## 1. 装包

```bash
dotnet add package Bing.MultiTenancy
dotnet add package Bing.AspNetCore.MultiTenancy     # Web 侧中间件与解析器
```

## 2. 配置解析链

内置贡献者（执行顺序 = 列表顺序）：`CurrentUser` → `Header` / `QueryString` / `Route` / `Domain` / `Cookie`。

```csharp
using Bing.MultiTenancy;
using Bing.AspNetCore.MultiTenancy;

services.Configure<BingTenantResolveOptions>(o =>
{
    // 方式一：直接用内置的 Header 解析器
    o.TenantResolvers.Add(new HeaderTenantResolveContributor());

    // 方式二：按域名解析（框架提供了这个便捷扩展，会插在 CurrentUser 之后）
    o.AddDomainTenantResolver("{0}.example.com");
});
```

> ⚠ 框架**刻意不提供 `UseMultiTenancy` 便捷扩展**——解析链是安全敏感的准入逻辑，要求你显式出现在 `Startup` 里。

## 3. 注册中间件

```csharp
app.UseMiddleware<MultiTenancyMiddleware>();   // 请求内用 ICurrentTenant.Change(id, name) 切换
```

> 中间件基于 `AsyncLocal` 做作用域隔离，多线程/异步场景下不会串租户。

## 4. 🔴 EF Core：重写 `CreateFilterExpression` 加租户过滤

**关键事实**：你可能以为「继承 `FilterBase<T>` 注册进 `IFilterManager` 就会自动生效」——**不会**。`UnitOfWorkBase.ConfigureGlobalFilters` 的开关是 `FilterManager.IsEntityEnabled<TEntity>()`，而该方法**硬编码只查 `ISoftDelete` 过滤器**（`FilterManager.cs:90-94`）。内置生效的过滤器只有软删除。

正确做法是**重写 `protected virtual CreateFilterExpression<TEntity>()`**：

```csharp
using System.Linq.Expressions;
using Bing.Datas.EntityFramework.Core;

public class AppUnitOfWork : UnitOfWorkBase, IAppUnitOfWork
{
    private readonly ICurrentTenant _currentTenant;

    // 让实体实现这个接口
    public interface ITenantEntity { Guid? TenantId { get; } }

    protected override Expression<Func<TEntity, bool>> CreateFilterExpression<TEntity>()
    {
        // 1) 先拿基类的软删除表达式
        var softDelete = base.CreateFilterExpression<TEntity>();

        // 2) 该实体不是租户实体，直接返回软删除
        if (!typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity)))
            return softDelete;

        // 3) 拼租户表达式：e => ((ITenantEntity)e).TenantId == CurrentTenantId
        var param = Expression.Parameter(typeof(TEntity), "e");
        var tenantId = Expression.Property(
            Expression.Convert(param, typeof(ITenantEntity)), nameof(ITenantEntity.TenantId));
        var constant = Expression.Constant(_currentTenant.Id, typeof(Guid?));
        var tenantExpr = Expression.Lambda<Func<TEntity, bool>>(
            Expression.Equal(tenantId, constant), param);

        // 4) 两者都为空 → 不过滤；任一为空 → 返回另一个；都有 → AndAlso
        if (softDelete == null) return tenantExpr;
        return Combine<TEntity>(softDelete, tenantExpr);
    }

    // 标准 LINQ 表达式组合（不依赖任何框架私有扩展）
    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> a, Expression<Func<T, bool>> b)
    {
        var p = a.Parameters[0];
        var right = new Replacer(b.Parameters[0], p).Visit(b.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(a.Body, right), p);
    }

    private sealed class Replacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from, _to;
        public Replacer(ParameterExpression from, ParameterExpression to)
        { _from = from; _to = to; }
        protected override Expression VisitParameter(ParameterExpression node)
            => node == _from ? _to : base.VisitParameter(node);
    }
}
```

> 框架自己的 `GetSoftDeleteFilterExpression` 用的是 `Bing.Expressions` 命名空间下的 `.Or(...)`（来自外部 `Bing.Utils.*` 包）。本配方用标准 `Expression` API 组合，避免依赖未公开的扩展方法。

## 5. Bing.Data.Sql + Dapper：注册 `ISqlTenantFilterContributor`

这一支提供了 `TenantIdFilter`，但需要你注入扩展点（`AddSqlCore` 默认只注册 `IsDeletedFilter`）：

```csharp
public class TenantContributor : ISqlTenantFilterContributor
{
    private readonly ICurrentTenant _tenant;
    public TenantContributor(ICurrentTenant tenant) => _tenant = tenant;

    // 该实体是否属于租户隔离范围 / 当前租户值 —— 按接口契约实现
}

services.AddScoped<ISqlTenantFilterContributor, TenantContributor>();
services.AddScoped<ISqlFilter, TenantIdFilter>();   // 过滤器本身
```

> ✅ 这一支有个好设计：`TenantIdFilter` 在**租户值缺失时拒绝渲染**（而不是放行），防止无租户边界的查询穿透隔离。

## 6. 分表场景别忘了缓存键

如果按租户分表（`orders__tenantA`），**实体映射缓存键不含 `TenantId`**（`GetCacheTableRouteKey` 只取 `mappingOptions?.TableRouteKey`）。必须把租户标识编进 `TableRouteKey`，否则不同租户会命中同一条缓存——**既是性能问题也是正确性问题**。详见 [性能指南 §2](../../operations/性能指南.md)。

## 注意事项

1. 🔴 **解析 ≠ 隔离**。本配方第 4、5 步是**必做项**，不是可选项。
2. 🔴 **FreeSQL 支没有任何租户过滤机制**，只能自行申请 `GlobalFilter`（`freeSql.GlobalFilter.Apply<T>(...)`）。
3. **解析链顺序写错会改变优先级**（如 Cookie 排在 CurrentUser 之前），症状很隐蔽。
4. **写入也要带租户**：过滤只管查询。新增实体时记得赋值 `TenantId`，否则这条数据对所有租户可见或不可见。
5. 需要临时跨租户查询时，用 `IDataFilter` 关闭过滤器（作用域内有效），用完恢复。

## 相关

- [安全指南 §6](../../operations/安全指南.md)（多租户隔离）
- [ADR-0006](../../architecture/adr/ADR-0006-multi-tenancy-resolve-chain.md)（为什么刻意不做便捷封装）
- [能力矩阵 §8](../../getting-started/能力矩阵.md)（三支都不行的部分）
- [性能指南 §2](../../operations/性能指南.md)（缓存键与 `TableRouteKey`）
