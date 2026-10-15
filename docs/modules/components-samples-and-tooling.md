# 组件、样例与部署资产

> **适用版本**：框架 `7.0.0`｜源码核对：2026-10-13

## 功能说明

仓库包含支付与 OAuth 组件、SQL/领域分析器、Admin 分层参考实现、WebApi/Hangfire/WinForms 样例，以及 Docker/Nginx、Kubernetes 蓝绿模板。这些资产的成熟度不同，不能从“工程存在”推断所有业务分支均可用。

## 核心实现

- 业务组件：`components/src/`
- 分析器：`framework/src/Bing.Data.Sql.Analyzers/`、`Bing.Ddd.Domain.Extensions.Analyzers/`
- Admin：`modules/admin/`
- 样例：`samples/`
- 部署：`deploy/bluegreen/`、`deploy/migrations/`

## 用法

组件入口和 Provider 支持范围见[组件库文档](../guides/组件库文档.md)。Admin 用于查看分层、工作单元、CAP 和两套 ORM 的组合；最小宿主优先从 `samples/Bing.Samples.WebApi` 开始。

```powershell
dotnet run --project samples/Bing.Samples.WebApi/Bing.Samples.WebApi.csproj
```

## 依赖与配置

- Admin 需要外部注入数据库、Redis、RabbitMQ、JWT 和日志服务配置。
- Hangfire 样例需要持久化与后台任务配置。
- 蓝绿脚本依赖 Docker Compose/Nginx 或 Kubernetes 环境。

## 已知限制与注意事项

- **待实现**：支付和 OAuth 的部分工厂/Provider 分支仍抛 `NotImplementedException`。
- **待确认**：Admin 的外部 MySQL/CAP/RabbitMQ 全量链路取决于专用测试环境。
- **待确认**：部署模板存在不等于目标集群已完成切流、排空、回滚和强杀恢复演练。
- WinForms 样例不在主解决方案内，需单独构建验证。
