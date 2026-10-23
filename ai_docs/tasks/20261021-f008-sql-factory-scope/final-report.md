# F008 独立验收最终结果

**Round 2：PASS，F008 CLOSED，OPEN_ACTIONABLE=0。** 原两类作用域故障修复成立，五工厂scope、缓存与兼容注册契约完成本地验收。HEAD为`5065c38ced05a95088e17d89d96c9a1610e37159`加当前未提交候选；本轮仅测试变化，生产/fixture/consumer/benchmark与首次验收哈希一致。

| Finding | 结论 | 新增直接证据 |
|---|---|---|
| FIX-001 | CLOSED | Begin/BeginAsync事务孩子typed Query+实体Delete、A/B filter、数据源快照；EF Shared/Independent真实adapter、完整SQL/参数/行集、Owned/External |
| FIX-002 | CLOSED | 新容器唯一实体，经scoped factory冷miss/热hit/跨scope Singleton cache复用、DbKey/Profile隔离、命中后动态filter |
| FIX-003 | CLOSED | 五工厂预注册type/factory/instance及Singleton保留、后注册解析/集合；多Provider正逆序/重复注册/子scope身份与完整具名路由SQL |

独立Fresh Release full：Core net8 **168/0/0**；SqlServer含多Provider net8 **297/0/0**；SQLite integration net8 **167/0/1 gated**；EF实际net6 **100/0/0**。最小新增链SQLite3项、EF4项均通过，已包含在full，不额外累计。命令有界、退出0，无超时，stdout/stderr/TRX齐全；diff --check通过。

同候选Fix证据复用：Core net6 **168/0/0**、SqlServer net6 **297/0/0**、SQLite integration net6 **167/0/1 gated**。原受控SQLite evidence case双TFM各1通过，正是full中skip的那项；每TFM合并最多168个不同case，不叠加旧164或重复轮次。

其他REUSED_UNCHANGED_SCOPE：Data双TFM各157、Data.Sql各1317、CustomProvider各21；MySQL各180、PostgreSQL各138、Oracle各93；SQLite unit各112；FreeSQL各76通过/26跳过；Admin EF net6为140/36、Admin FreeSQL net6为150/35。相关消费者构建和迁移示例编译证据沿用首轮，配置与来源明细在review.md。不重复全仓85构建/11796旧矩阵或五Provider全矩阵。

原故障before为2失败，当前full的ValidateScopes=true/false typed回归通过。事务Delete对已删除行的0→1是影响行/过滤证据；实际改变数据的回滚恢复另由既有事务测试证明。EF AsyncLocal按当前调用捕获diagnostics，完整SQL/参数和行集断言保留；空参数的Count=0是现有公开诊断表示，并非SQL参数。未为绿弱化关键断言，也未修改生产源码。

性能scope复用：原net8 Release/ValidateScopes probe两个场景各warmup100+3×1000，总**0.4127652秒**、零异常/连接resolver调用；**24/24 Dry smoke**走真实benchmark基类及显式scope。仅为本地成本/执行证据，非性能预算或P95结论；未重跑probe/smoke，未补造原probe缺失的外层退出码。

迁移限制：Singleton/后台每业务CreateScope；factory绑定解析scope，不能跨scope缓存；工厂Create产物由调用方释放，scope覆盖执行/枚举/reader/清理，事务孩子在事务结束前完成。IDataFilter仍Scoped、无状态cache/registry为Singleton。自定义Singleton由注册者负责依赖；raw SQL/Multiple不自动加filter，Oracle Multiple及Doris只读无事务边界不变。

```text
IMPLEMENTATION: PASS
TEST: PASS_LOCAL_REQUIRED_SCOPE
PERFORMANCE_EVIDENCE: PASS_LOCAL_PROBE_AND_DRY_SMOKE_ONLY
RESOURCE_EVIDENCE: NOT_RUN_OUT_OF_SCOPE
EXTERNAL_GATE: BLOCKED_EXTERNAL_GLOBAL_GATES
RELEASE: LOCAL_F008_PASS_GLOBAL_RELEASE_GATES_INDEPENDENT
GOAL: COMPLETED
F008_STATUS: CLOSED
OPEN_ACTIONABLE: 0
Next Action: STOP
```

Completed：三FIX及本地计划7/7验收。Open Actionable：无。Blocked Approval：本地F008无，Scoped已获批准。Blocked External：外部实库/CI/CodeSmith等仍独立未运行；正式资源性能矩阵亦未运行。Not Applicable：重复未变矩阵/基线。Accepted Limitations：Provider与所有权边界。Verified Boundaries：本地无DB probe及Dry执行范围。Deferred：退款/全局规则其他遗留及整体发布，由父会话独立处理。

旧review/final-report原文快照已保存于`artifacts/review-round-2/round-1-review.md`、`round-1-final-report.md`，Luna execution及历史证据未覆盖。新证据仅在review-round-2。完整验收、精确证据路径、复用边界和Round索引见[review.md](review.md)。父会话可更新整体F008遗留状态；本会话未改旧merged-code-audit历史报告。
