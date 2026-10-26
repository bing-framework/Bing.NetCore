# GC 调查模型调整与继续修正授权

日期：2026-10-23（Asia/Shanghai）。

用户在限定 Review 4 交回后明确要求：

> 好的，继续修正功能，由于 GC 根因仍未确定，因此 这块的排查 使用 @Codex 的 gpt-6-astra / xhigh 来排查一下，不用 opencode 去排查。

本次明确指令恢复剩余 FIX-003 修正及 GC 根因调查，取代上一轮限定收尾后的停止要求；不需要重复请求相同范围授权。仍遵守 GOAL_RULES 的实验限次、影响分析和无进展停止规则，不因换模型重置历史实验次数。

- GC 根因调查：新建 Codex / GPT-6-Astra / xhigh 委派。负责分析两个真实失败用例、历史证据、可证伪假设及必要的最小受控复现，交付依据与具体修复方案；不交 OpenCode 调查。先写简短续查计划再执行，不停在等待计划批准。
- 已知工具修正：原 OpenCode / deepseek-v4.1-flash 仅处理 Review 4 的 FIX-003 剩余 AC。其所有权限定为诊断 runner、timeout control 及工具修正报告/虚拟证据，不开展 GC 试跑或推测性生产修复。
- 独立验收：沿用 GPT-6.1-Sol / high。FIX-001/002 已关闭，未有新影响不重开；诊断工具结果与产品 GC 结果分别判定。

工具修正只需落实 build 准备后的最后 deadline 准入及实际剩余等待、覆盖 monitor 准备及 build 等待异常的所有权清理、多个本次进程共享的独立有界清理预算。保留已通过的 test/PID/collector 行为，验证只用最小静态与有限虚拟场景。

Astra 使用独立的 astra-investigation-plan.md、astra-diagnosis.md 及 artifacts/astra-investigation/；不改原 review.md、历史 raw 或 Stage B 报告，不与工具修正者并写同一文件。必要实验须先记录具体假设、与历史实验的差异、观测目标、预设截止和通过/失败判据；同类实验至多一次诊断与一次确认。已反复执行的无新观测点全量/压力不能因切换模型再运行。允许本次新假设所需的精准编译、回归与最小诊断；保持断言、并行设置和预算，不能用延长等待或额外 GC 将原失败变绿。需要使用仍在修正的工具时先等待其交付。

若证明产品或测试存在缺陷，先交付可重复失败回归和最小修复建议，再按已经授权的修复目标实施并独立验收；证据不足则明确未定及缺失证据，不猜测修改生产。当前 ROOT_CAUSE=NOT_DETERMINED、GC_PROBLEM=UNRESOLVED，正式 build PASS、solution/Release FAIL 仍保留，直到有相应新证据。

保留既有工作区修改，UTF-8 读写；不暂存、提交、推送、重置或清理 Git。不改 SDK、TFM、依赖版本、API baseline 或性能预算，不停止无关进程，不发送外部通知。不扩展到 Uow/CAP/FreeSQL 或路线图后续阶段。
