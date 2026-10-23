# Artifact Manifest

当前最终结论：Sol/high 第 2 轮独立审查 **PASS**，两项 Finding 全部 CLOSED，OPEN_ACTIONABLE=0。最新新鲜 net6/net8 Release 各 8/8 及正常宿主探测证据见 [Review 2 清单](artifacts/review-round-2/manifest.md)；结论见 [review.md](review.md)。下方保存实施及修复轮历史证据。

初始执行轮次的证据对应其候选；基础提交为 `5065c38ced05a95088e17d89d96c9a1610e37159`，代码范围摘要与程序集 SHA256 见 [candidate-identity.txt](artifacts/candidate-identity.txt)。Review Fix Round 1 的最终 Sample/Test 通过证据使用独立目录，修改文件 SHA256 见该目录中的清单。日志包含原始命令和实际退出码，历史尝试保留且不覆盖。

## Review Fix Round 1

Review Fix 的代码与测试证据单独保存在 [artifacts/review-fix-round-1](artifacts/review-fix-round-1)，不会覆盖原始 Review 或原执行证据。Round 1 在原 6 用例基础上新增并发只读 HTTP 理论场景，最终 net6/net8 各执行 8 用例；后续 Sol/high 第 2 轮独立审查已通过。

| Artifact | 内容 / 状态 |
| --- | --- |
| [change-impact-analysis.md](artifacts/review-fix-round-1/change-impact-analysis.md) | Round 1 CIA：Sample/Tests 路径、运行时影响、TFM、风险和测试范围 |
| [source-files-sha256.txt](artifacts/review-fix-round-1/source-files-sha256.txt) | 本轮修改的 8 个 Sample/Test 文件哈希；明确不代表整个仓库候选身份 |
| [net6-release-final-2/net6-release-final-2.log](artifacts/review-fix-round-1/net6-release-final-2/net6-release-final-2.log) 与 [TRX](artifacts/review-fix-round-1/net6-release-final-2/net6-release-final-2.trx) | 最终 net6.0 Release 全套，8 通过、0 失败、0 跳过，退出码 0 |
| [net8-release-final-2/net8-release-final-2.log](artifacts/review-fix-round-1/net8-release-final-2/net8-release-final-2.log) 与 [TRX](artifacts/review-fix-round-1/net8-release-final-2/net8-release-final-2.trx) | 最终 net8.0 Release 全套，8 通过、0 失败、0 跳过，退出码 0 |
| [sample-release-build-final-2/sample-release-build-final-2.log](artifacts/review-fix-round-1/sample-release-build-final-2/sample-release-build-final-2.log) | 最终 Sample Release Build，退出码 0、0 错误；保留 net6.0 SDK 支持期提示 |
| [git-diff-check-final.log](artifacts/review-fix-round-1/git-diff-check-final.log) | `git diff --check` 退出码 0；有一条工作区既有换行提示 |
| [hangfire-scope-fix-net6/hangfire-scope-fix-net6.log](artifacts/review-fix-round-1/hangfire-scope-fix-net6/hangfire-scope-fix-net6.log) 与 [TRX](artifacts/review-fix-round-1/hangfire-scope-fix-net6/hangfire-scope-fix-net6.trx) | 最终套件前的窄验证：Dapper/EfBridge 真实 Hangfire Server 各 1 项通过；被完整套件复验 |
| `net6-release/`、`net8-release/` | 先前 8/8 运行记录；在最后移除未测量的固定队列指标后由 `*-final-2/` 同候选最终运行替代 |
| `diagnostic-*`、`net6-release-final-run2/`、`net6-release-final-run3/` | 修复过程的编译/断言/生命周期失败证据；保留供审计，不作为最终通过依据 |

## Initial Implementation Evidence (Round 0)

| Artifact | 内容 / 状态 |
| --- | --- |
| [artifacts-baseline-status.txt](artifacts-baseline-status.txt) | T01 起点完整 `git status --short --branch`，保护 F008 与前置 Uow 工作区变更 |
| [artifacts-baseline-diff-stat.txt](artifacts-baseline-diff-stat.txt) | T01 起点已跟踪 diff 统计 |
| [001-sample-release-build.log](artifacts/001-sample-release-build.log) | 初次 Sample Build 失败：Startup 缺少 `Bing.Dapper` using；随后修复 |
| [003-tests-net6-release.log](artifacts/003-tests-net6-release.log) | 初次 net6 测试编译失败：缺少 `System.Net.Http.Json` using；随后修复 |
| [004-tests-net6-retry.log](artifacts/004-tests-net6-retry.log) | 初始 6 个用例均发现直接 DbContext 模型调用基类自动过滤配置的依赖；Sample 改为显式映射 POCO 后解决，无框架代码修改 |
| [005-tests-net6-mapping-fix.log](artifacts/005-tests-net6-mapping-fix.log) | 中间候选 net6 6/6 通过；后续模式保护及测试断言加入后由最终运行替代 |
| [006-tests-net8-release.log](artifacts/006-tests-net8-release.log) | 中间候选 net8 6/6 通过；后续模式保护及测试断言加入后由最终运行替代 |
| [007-sample-release-build-final.log](artifacts/007-sample-release-build-final.log) | 最终 Sample Release Build，退出码 0 |
| [008-tests-net6-final.log](artifacts/008-tests-net6-final.log) | 最终 net6.0 Release E2E，6 通过、0 失败、0 跳过，退出码 0 |
| [final-net6.trx](artifacts/final-net6/final-net6.trx) | 最终 net6.0 TRX |
| [009-tests-net8-final.log](artifacts/009-tests-net8-final.log) | 最终 net8.0 Release E2E，6 通过、0 失败、0 跳过，退出码 0 |
| [final-net8.trx](artifacts/final-net8/final-net8.trx) | 最终 net8.0 TRX |
| [010-static-check-final.log](artifacts/010-static-check-final.log) | 最终 diff check、尾随空白扫描、solution 项目和文档路径检查 |

最终 Build/Test 输出仍有 SDK 对要求目标 `net6.0` 的 NETSDK1138 提示；net8 测试另有既有 SQLitePCLRaw RID 资产 NETSDK1206 提示。最终候选无代码编译警告。没有触及生产 SQL 热路径，没有运行 Benchmark，也没有宣称性能变化；先前性能结果未用于本任务性能结论。
