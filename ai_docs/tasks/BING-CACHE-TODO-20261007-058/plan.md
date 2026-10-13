# 缓存批量响应字节预算 Provider 证据计划

## 目标

关闭前序缓存计划中“按字节自适应拆分”仍缺少真实 Provider 验收证据的 TODO，并修复 Hybrid 命中 null 值回填本地层的调用链缺陷。当前生产代码已经在 FreeRedis、CSRedis 和 EasyCaching Redis/Hybrid 的批量读取路径接入 `CacheBatchAdaptive`：当一个批次的原始响应超过 `MaxBatchPayloadBytes` 时，递归拆分并重读；单个值仍超限时必须失败。本轮补充真实 Redis 集成契约，确认多值合计超限会拆分，并保持 List 与 Stream 的键顺序、重复键、缺失命中状态和值一致。Hybrid 对分布式层命中但值为 null 的项必须保留 `Hit=true`，清除本地项并跳过本地 null 写入。

## 范围

- 在共享 `CacheCoordinationAssertions` 增加多值批量负载拆分断言。
- 修复 EasyCaching Hybrid null 命中回填：不向 EasyCaching 内存 Provider 写入 null，仍返回命中结果。
- 在 FreeRedis、CSRedis、EasyCaching Redis 和 EasyCaching Hybrid 的现有集成入口调用该断言。
- 复用已有随机命名空间、集成测试门控和清理逻辑。
- 更新本任务 TODO、执行报告和生产符号到测试方法映射。

## 非目标

- 不修改公共 API、键格式、租约、拓扑路由或性能预算；生产变更仅限 Hybrid 回填 null 的条件分支。
- 不重跑已经触发 No Progress Guard 的性能矩阵。
- 不把单节点 Redis 集成结果解释为三主三从 Cluster 或客户端分片的外部证据。
- 不把“单值超限明确失败”改为静默截断或放宽资源预算。

## 验收标准

1. 配置 `BatchSize >= 2`、`MaxBatchPayloadBytes` 小于两个值的合计且大于任一单值时，Provider 的 List API 返回全部值。
2. 同一输入通过 Stream API 返回相同键顺序、重复位置、值和 `Hit` 状态。
3. 缺失键和 `null` 值仍能区分；单值超过预算继续由既有资源边界断言明确失败，不在本共享断言中重复写入路径。
4. FreeRedis、CSRedis 和 EasyCaching Redis/Hybrid 的门控集成测试在本地非生产 Redis 通过；未配置环境时保持跳过。
5. `git diff --check` 通过，任务文档记录外部拓扑证据仍为 BLOCKED_EXTERNAL/DEFERRED。

## 变更影响分析

```text
ChangedFiles: framework/src/Bing.EasyCaching/Bing/EasyCaching/CacheManager.Hybrid.cs；framework/tests/CacheCoordinationAssertions.cs；FreeRedis/CSRedis/EasyCaching 集成测试入口；本任务 ai_docs 记录
ChangedProjects: Bing.Caching.FreeRedis.Tests.Integration、Bing.Caching.CSRedis.Tests.Integration、Bing.EasyCaching.Tests
ChangedPublicContracts: 无
ChangedRuntimePaths: EasyCaching Hybrid 批量回填对命中 null 值跳过本地 Set、改为本地 Remove
ChangedProviders: FreeRedis、CSRedis、EasyCaching Redis、EasyCaching Hybrid（测试证据）
ChangedTFMs: 受影响测试项目的 net6.0/net8.0
ChangedBuildPackaging: 否
ChangedBenchmarkHarness: 否
ChangedDocsOnly: 否（生产分支、测试和任务记录）
AffectedDependents: 共享测试断言调用方；无生产调用方
RiskLevel: MEDIUM
```

## 验证顺序

1. 共享断言编译和精准集成测试：FreeRedis、CSRedis、EasyCaching。
2. 受影响测试项目 net6.0/net8.0 精准测试或项目测试（按环境门控结果记录）。
3. `git diff --check`、UTF-8 检查和任务文档协议检查。

## 状态分类

- 本地可执行项：补充真实 Provider 的多值自适应拆分证据。
- 已实现但需保留边界：生产自适应拆分实现；单值超限、非快照和额外 Redis 命令语义。
- BLOCKED_EXTERNAL：三主三从 Cluster、独立客户端分片、多进程故障转移下的同类证据。
- DEFERRED：真实传输往返/客户端内部排队观测和严格三方性能对照。
