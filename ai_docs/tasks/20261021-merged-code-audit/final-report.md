# 第 2 次独立验收报告（Fix Round 1 后）

**通过：I01–I07 为 7/7，FIX-001–006 为 6/6 CLOSED；OPEN_ACTIONABLE=0。** 实现和批准的本地测试通过；发布仍 BLOCKED，自动 Review/Fix 循环停止。

```text
IMPLEMENTATION_STATUS: PASS
TEST_STATUS: PASS (local scope)
PERFORMANCE_EVIDENCE_STATUS: REUSED_UNCHANGED_SCOPE / DRY_SMOKE_ONLY; FORMAL_BLOCKED_EXTERNAL
RESOURCE_EVIDENCE_STATUS: BLOCKED_EXTERNAL
EXTERNAL_GATE_STATUS: BLOCKED_EXTERNAL
RELEASE_STATUS: BLOCKED
GOAL_STATUS: STOPPED_BLOCKED
```

候选 HEAD `a99a16c4b55c407cc81d771822667044e2e9e9b1` 加完整当前 diff/未跟踪新测试。独立从当前源文件创建初始无输出的隔离候选；所有 --no-build DLL 均由本候选 fresh 主方案 build 生成，TRX 实际 source DLL 逐项匹配。HEAD/diff/新测试最终无漂移。旧两报告原文快照保存于 [re-review-round-1](artifacts/re-review-round-1/previous-review.md)，历史证据、execution.md 和业务/测试/工程源码均未修改。

## fresh 构建准确范围

- SDK 8.0.424 / MSBuild 17.11.48，net6 6.0.36、net8 8.0.30 可用；与上轮版本/环境一致。
- 主方案 restore、Release build 均 exit 0；**232 warnings / 0 errors**。当前 **156 个直接项目 / 219 个声明项目-TFM 单元**均启用 Release Build.0，实际产物及本次日志记录全部核对。
- **85 个生产项目 / 89 个声明 TFM 单元**全部覆盖：70 framework、3 components、12 Admin；不存在 solution 外遗漏生产项目。未把 ai_docs 下任何隔离副本算入项目数量。
- Core/AOP/RedisRateLimit/Ordering/Admin 选定 fixture/probe 的 **32 个 Release 产物检查全部存在**；AOP 双 TFM 和两 Admin probe 的复制与源 DLL 一致。两 Admin 空产物目标分别按预期 exit 1，未吞错。
- solution 外 Winform **REUSED_UNCHANGED_SCOPE，本轮未重跑**：上轮独立 net6.0-windows/win-x64 restore/build exit 0，1 warning/0 errors；源/依赖实现/构建属性和环境未变，未启动 GUI。
- CodeSmith net45 **BLOCKED_EXTERNAL / REUSED_UNCHANGED_SCOPE**：旧 MSB3644 仍有当前缺引用程序集证据。不能声称含旧工具的全仓完整编译。

[构建覆盖](artifacts/re-review-round-1/solution-build-coverage.json)、[主构建原始日志](artifacts/re-review-round-1/solution-build.stdout.log)、[复用理由](artifacts/re-review-round-1/evidence-reuse.json)。主方案告警不与上轮 Winform 告警合并冒充 fresh 数值。

## fresh 本地测试

**43 项目、81 个项目/TFM 单元：11,796 通过 / 0 失败 / 285 跳过；实际执行 11,796，发现 12,081。** 80 条 test 命令、81 份非空 TRX，全体 exit 0、无 runner 超时。没有把旧 37 fail 与新局部通过相减；没有额外补建后替换默认入口结果。

| 关键范围 | 本轮结果 |
|---|---|
| AOP 全项目 net8/net6 | 各 102 通过；真实插件调用/无拦截/ALC 卸载保留，使用本次 Release fixture |
| 两 Admin 全本地套件 | EFCore 140 通过/36 跳过；FreeSQL 150 通过/35 跳过；各 16 个真实清理/脱敏用例全过，三个 Hangfire 角色断言通过；未运行 probe DB 入口 |
| AssertHelper net8/net6 | 各 16 通过；精确类型四路径、单次执行、实际异常返回、无关/派生拒绝、无异常及 keyword 断言 |
| WebApi 默认不带 -f 入口 | 两 TFM 各 1 条真实通过；业务 HTTP 响应、宿主停止和端口释放断言保留 |
| SQLite 全本地集成 net8/net6 | 各 **151 通过/0 失败/1 跳过**；仅跳过额外受控 Provider 证据记录 |
| MySQL 无库装配过滤项 net8/net6 | 各 1 通过；数据库项未运行 |
| Core / Validation / EventBus 双 TFM | 各 639 / 186 / 374 通过 |
| EFCore / FreeSQL | net6 EFCore 95 通过；FreeSQL 双 TFM 各 76 通过/26 跳过 |
| Bing.Tests 安全定向过滤 | 双 TFM 各 7 通过；旧聚合其余用例未运行 |

逐项目、逐 TFM、全部跳过原因及未运行归属：[test-summary.md](artifacts/re-review-round-1/test-summary.md)、[test-summary.json](artifacts/re-review-round-1/test-summary.json)。I05 遗漏的 10 项全部纳入且实际运行；AOP/Events/EFCore/FreeSQL/Validation 范围全部包含。

285 跳过：EasyCaching 140、FreeSQL 52、Admin EFCore 36、Admin FreeSQL 35、MailKit 10、Auditing 6、MVC/Payments/SQLite 各 2。**未运行另计**：CSRedis/FreeRedis 实机集成，Doris/Oracle/PostgreSQL/SQLServer 实库，未门控的 Serilog/Seq 实机集成，MySQL 剩余数据库用例，两个旧聚合项目过滤范围外用例。资源/共享 fixture/Backup 不当独立套件。外部门控保持关闭，未启动服务、访问生产库或发起真实支付；Admin 本地注册构造 localhost 客户端，不声称零网络尝试。

## 七项与六 FIX

| 实现项 | 当前验收 |
|---|---|
| I01 / I03 / I06 | CLOSED，相关实现 scope 未变；本轮构建和对应本地回归仍通过 |
| I02 | CLOSED；精确类型与异常助手回归已补齐 |
| I04 | CLOSED；当前配置/TFM 复制、两 Admin probe 图及真实生命周期验证正确 |
| I05 | CLOSED；10/10 覆盖，全部本地矩阵通过，WebApi 默认双 TFM 真实发现 |
| I07 | CLOSED；三格修正与真实实现、限制和未验收状态一致 |

FIX-001、FIX-002、FIX-003、FIX-004、FIX-005、FIX-006 **逐条 CLOSED**；根因、文件行、修复要求和当前验收证据完整保留在 [review.md](review.md)。

## 性能、功能缺口与风险决策

SQL/缓存热路径和 harness 没变，**REUSED_UNCHANGED_SCOPE** 复用初审 1 个 SQL CI、3 个 SQLite E2E **Dry smoke**，不重跑无关 benchmark。只证明路径能运行；不代表稳态/P95/吞吐、性能提升、正式预算或资源/容量通过。

**F008 仍是已复现且未修复的真实 SQL 根容器捕获缺陷，BLOCKED_APPROVAL。** 正常子 scope 可能丢失 Scoped IDataFilter 状态或触发 ValidateScopes；现有 SQLite 通过不覆盖它。需先决定工厂生命周期与消费者兼容策略，本轮未擅自改 DI 契约。

确定未实现能力仍为 **2 项：微信退款、领域全局验证规则**；另有 **2 个待确认候选：OAuth 统一工厂、按租户自动选择文化**，均保留 BLOCKED_APPROVAL，不自行实现或扩大数量。真实外服务/CI、旧 CodeSmith、正式性能与资源证据仍 **BLOCKED_EXTERNAL**。L01–L05 现有支持限制不是新的发布例外。

Completed：7 项实现、6 个 FIX、fresh checkpoint 与完整本地矩阵。Open Actionable：**0**。Blocked Approval/External：如上。Not Applicable：本轮性能提升 before/after；Verified Boundaries：无新增容量边界；Deferred：无关 TODO/DocFX。No-Progress Check：CHANGED；Next Action：**STOP**。本地实现通过不能代替发布风险决策，不因 Release Blocked 自动再开 Fix。

仓库 diff check、文档索引和文档校验命令全部 exit 0；文档校验 **13 项全部通过**。结果保存在本轮证据目录；只改报告后不重跑代码套件。

报告绝对路径：

- `H:\Bing_Framework\Bing.NetCore\ai_docs\tasks\20261021-merged-code-audit\review.md`
- `H:\Bing_Framework\Bing.NetCore\ai_docs\tasks\20261021-merged-code-audit\final-report.md`
- 新证据：`H:\Bing_Framework\Bing.NetCore\ai_docs\tasks\20261021-merged-code-audit\artifacts\re-review-round-1\`
