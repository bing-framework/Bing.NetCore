# 功能与文档对照清单

> **适用版本**：框架 `7.0.0`｜源码与文档核对：2026-10-13

本清单按职责域核对生产源码与文档，不把测试工程、源码生成产物或同一能力的 Provider 包重复计为独立功能。状态以当前工作树为准。

## 对照结果

| 功能域 | 涉及工程或目录 | 实现状态 | 文档状态 | 结论与入口 |
| --- | --- | --- | --- | --- |
| 核心、模块图与生命周期 | `Bing.Core` | 已实现 | 完整 | [模块化与插件](../modules/modularity-and-plugins.md) |
| 启动期插件与热更新 | `Bing.Core/Modularity/PlugIns`、`Bing.PluginRuntime` | 已实现，有限制 | 完整 | 可信进程内插件；不提供不可信隔离 |
| 约定 DI、Lazy 与属性注入 | `Bing.Core`、`Bing.Aop.AspectCore` | 已实现，有限制 | 完整 | [依赖注入与 AOP](../modules/dependency-injection-and-aop.md) |
| 参数验证与工作单元 AOP | `Bing.Validation*`、`Bing.Ddd.Application.AspectCore` | 已实现 | 完整 | 需显式启用 AspectCore 执行器 |
| Web、MVC 与统一响应 | `Bing.AspNetCore*`、`Bing.AspNetCore.Mvc*` | 已实现 | 已补齐 | [Web、安全与多租户](../modules/web-security-and-multitenancy.md) |
| JWT、当前用户与权限模型 | `Bing.AspNetCore.Authentication.JwtBearer`、`Bing.Security`、`Bing.Permissions` | 部分实现 | 完整 | JWT 可用；通用权限检查器待实现 |
| 多租户解析 | `Bing.MultiTenancy*`、`Bing.AspNetCore.MultiTenancy` | 已实现，有限制 | 完整 | 解析当前租户，不等于 ORM 数据隔离 |
| EF Core | `Bing.EntityFrameworkCore*` | 已实现 | 完整 | 五个 Provider；详见[能力矩阵](../getting-started/能力矩阵.md) |
| FreeSQL | `Bing.FreeSQL*` | 已实现，有限制 | 完整 | MySQL；通用 IQueryable 异步入口可能同步计算 |
| SQL/Dapper | `Bing.Data.Sql`、`Bing.Dapper*` | 部分实现 | 完整 | Provider 缺口和 Lambda 多源限制已单列 |
| 领域事件、本地事件与 CAP | `Bing.Ddd.Domain`、`Bing.Events` | 已实现，有限制 | 已修正过时描述 | [事件与一致性](../modules/events-and-consistency.md) |
| 消费幂等与有序事件 | `Bing.Events/Consumption`、`Ordering` | 已实现 | 完整 | 需数据库表、稳定消费者名和业务事务 |
| 审计 | `Bing.Auditing*`、ORM 审计适配 | 已实现 | 完整 | 日志、HTTP、数据库、查询、归档和清理 |
| 锁与令牌桶限流 | `Bing.Core/Locks`、`Bing.Locks.CSRedis`、ASP.NET Core 限流 | 已实现，有限制 | 已补齐 | Redis 锁不能作为库存唯一正确性保障 |
| 缓存 | `Bing.Caching*` | 已实现 | 完整 | 内存抽象、CSRedis、FreeRedis；EasyCaching 除外 |
| 日志与诊断 | `Bing.Logging*`、Serilog、Exceptionless、SkyAPM、MiniProfiler | 已实现，有限制 | 完整 | HTTP 日志无通用字段级脱敏 |
| 邮件、本地化、映射与模板 | `Bing.Emailing`、`Bing.MailKit`、`Bing.Localization*`、`Bing.AutoMapper`、`Bing.TextTemplating*` | 已实现，部分需手工装配 | 已补齐 | [通用横切服务](../modules/cross-cutting-services.md) |
| 支付、OAuth 与 EasyCaching | `components/src/Bing.Biz.*`、`Bing.EasyCaching` | 部分实现 | 已标注 | 工厂分支未完成，EasyCaching 注册入口被注释 |
| 分析器与源生成器 | `Bing.Data.Sql.Analyzers`、`Bing.Ddd.Domain.Extensions.Analyzers` | 已实现 | 完整 | SQL 安全诊断和变更跟踪生成 |
| Admin、Samples 与部署模板 | `modules/admin`、`samples`、`deploy` | 参考实现；环境相关 | 已补齐 | [组件、样例与部署资产](../modules/components-samples-and-tooling.md) |

## 文档不符项

本轮已修正以下代码与文档冲突：

- EF Core 和 FreeSQL 已接入领域事件派发，旧文档仍写“生产代码零调用”。
- 本地和 Redis 令牌桶已经存在，旧安全指南仍写“无速率限制”。
- ADR 已增加至 14 份，旧导航仍写 10 份。
- FreeSQL 原 65 个异步占位异常已移除，现状是通用 IQueryable 路径可能同步计算。
- `docs/api/index.md` 原为 docfx 英文模板占位页，现已改为中文生成说明。
- `docs/README.en.md` 原为英文导航，现已提供中文译本并保留路径兼容。

## 待实现/待确认

| 项目 | 状态 | 依据 |
| --- | --- | --- |
| 不可信插件进程隔离 | 待实现 | 当前加载器执行宿主进程内可信代码 |
| 通用权限检查器和全局权限过滤 | 待实现 | `Bing.Permissions` 主要提供 Identity 模型和管理器 |
| 全 ORM 自动多租户过滤 | 待实现 | 当前只提供租户解析和显式扩展点 |
| XSS、CSRF 和 IP 白名单 | 待实现 | 未发现通用框架实现 |
| EasyCaching 注册入口 | 待实现 | 注册扩展源码被注释 |
| 支付/OAuth 全 Provider | 待实现 | 仍有明确 `NotImplementedException` 分支 |
| 蓝绿动态切流、回滚和强杀恢复 | 待确认 | 有模板与脚本，缺目标部署环境的本轮证据 |
| MySQL/CAP/RabbitMQ 全量集成 | 待确认 | 受专用环境和门控配置约束 |
| Oracle、Doris 全路径 | 待确认 | 当前文档与测试记录不足以证明生产可用 |
