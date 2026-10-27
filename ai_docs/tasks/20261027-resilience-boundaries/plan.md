# 容错边界能力补全计划

任务编号：`20261027-resilience-boundaries`
基线任务：`20261027-resilience-extensions`

执行状态：BND-01～BND-06 主体已交付；2026-10-27 复核新增 R01～R05 后，本轮完成加权选址、索引契约、生命周期诊断、Journal 保留清理和性能证据补全。新证据为 869 项回归、120 轮测量和 34 包独立消费，见 [execution.md](execution.md) Round 1；最终关闭状态以 [review.md](review.md) 为准。生产阈值和部署环境认证单独登记。

## 目标

补齐前次复审列出的宿主边界能力，并保持中性核心、三个 Provider、两个动态 API 客户端和既有错误契约不变。新增能力全部显式注册，不改变默认行为。

## 范围

1. 生产关系型 Journal 迁移脚本、只读结构校验，以及 EF Core/FreeSQL/Dapper 实际事务桥接；
2. 通过 HTTP 协议接入 Consul、Nacos、Kubernetes EndpointSlice 的可选服务发现适配器；
3. Replay/Saga 的通用管理查询、人工操作 HTTP 接入和可选操作控制台，继续由宿主提供授权；
4. 业务补偿示例（订单/库存风格的可替换处理器），不把示例当作通用业务语义；
5. Redis Sentinel/Cluster 故障验证脚本和网络故障边界报告；
6. Provider/Discovery/Replay/Saga 的可重复性能基线，不自行设定生产 SLA；
7. 修正运行时热切换迁移文档。

## 非目标

注册中心协议、管理控制台和 ORM 桥接保持可选，不进入中性核心的默认调用链。补偿交付可运行的订单/库存/支付示例，业务规则仍由宿主提供；不执行生产数据库迁移，不自行制定生产性能阈值。宿主仍负责凭据、授权、事务、部署和生产故障策略。

## 阶段

### BND-01 迁移与文档

- 现状：`RelationalOperationStore.InitializeAsync` 仅供迁移流程，生产脚本缺失；热切换文档曾将运行时切换和重启切换混写。
- 修改：提供 SQLite/MySQL v1 SQL、只读结构校验、迁移 README；增加可选 EF Core/FreeSQL 桥接，Dapper 复用 ADO.NET 事务；修正文档。
- 验证：SQLite 内存库执行脚本；脚本结构与 `InitializeAsync` 一致；文档 UTF-8 检查。
- 验收：宿主可在发布前执行版本化迁移，应用启动不需建表。

### BND-02 服务发现适配

- 现状：只有配置源与受控 HTTP 目录源。
- 修改：扩展无厂商 SDK 依赖的 `Bing.Resilience.Discovery`（维持显式创建来源），通过受控 HTTP、ETag/版本和统一可信策略读取 Consul、Nacos、Kubernetes EndpointSlice；不引入厂商 SDK。
- 验证：协议响应解析、空集合、过期、身份复用、Origin 校验、失败回退。
- 验收：三个适配器输出相同 `ServiceEndpointSnapshot`，未配置时不注册。

### BND-03 管理与业务补偿样例

- 现状：Replay/Saga 已有协调器接口，但没有宿主 HTTP 接入样例。
- 修改：增加 ASP.NET Core 可选管理包和控制台，暴露查询、取消、人工动作；增加持久订单/库存/支付样例处理器，使用显式幂等键和确认路径。
- 验证：授权、版本 CAS、Unknown 先确认、重复补偿和审计。
- 验收：样例不绕过协调器、不暴露载荷和密钥、不把示例业务作为默认注册。

### BND-04 Redis 故障边界

- 现状：单 Redis 真实进程已验证，未覆盖 Sentinel/Cluster/网络分区。
- 修改：新增任务独占 Docker Compose/脚本与故障矩阵；不修改生产 Redis 客户端行为。
- 验证：pause/unpause、连接中断、主从切换（环境支持时）、重复上报和租约过期。
- 验收：报告明确 AuthoritativeRead、FailClosed/FailOpenWithLocalCircuit 的边界，不宣称跨分区强一致。

### BND-05 性能证据

- 现状：已有资源探针，无生产阈值。
- 修改：提供固定 workload 的 provider、switch、discovery、replay/saga 采样脚本。
- 验证：双框架、固定种子、记录吞吐/延迟/分配/资源回收；不伪造 before/after。
- 验收：报告测量值和限制，阈值仍为 BLOCKED_APPROVAL。

### BND-06 收口

- 更新 `execution.md`、`test-map.md`、迁移文档和本任务报告；运行直接受影响测试、扩展项目回归和包消费。
- 完成标准：新增生产符号有测试映射；没有 OPEN_ACTIONABLE；外部生产门禁单列。

## 复核增量范围（Round 1）

按复核报告的 FIX-BND-R01～R05 补齐已批准上游范围，不重新执行无关阶段。交付平滑加权默认选择器及可选 Ordered、MySQL 认领索引名称校验、统一生命周期事件、默认关闭的终态载荷/历史保留清理，以及 HTTP/Redis/SQLite/MySQL 固定负载新证据。保留幂等身份与未知/在途保护；不升级全局依赖或自行制定生产 SLA。
