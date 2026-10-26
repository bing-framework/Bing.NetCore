<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: 20261025-qiniu-file-storage
AI_REVIEWED_AT: 2026-10-26T02:18:56Z

# 七牛文件存储独立复审：Round 2 修复验收

审查日期：2026-10-26（Asia/Shanghai）。本轮对照完整计划、执行记录、上次 review、工作区/暂存区差异、实际代码和验证产物复审。主审检查容量工具实际调用链；新的 luna_worker 独立检查文件系统查询、测试断言及产物身份。仅更新本报告和本轮审查证据，未修改生产代码、既有测试、plan.md 或 execution.md。

前一版 NEEDS_FIX 报告保存在 [review-before.md](evidence/re-review-after-round-2/review-before.md)。

## 结论

FIX-003 已满足本轮修复验收条件，可以关闭。此前关闭的 FIX-001/002 相关代码未变化，结论继续有效。本轮未确认新的 MUST_FIX 或 SHOULD_FIX。

当前为 PASS_WITH_ISSUES：本地实现和已验证范围通过，剩余 OPT-001 为延期可选性能优化。七牛真实云端及容量验收仍未完成，代码审查通过不代表目标云空间已完成上线验收。

| 状态维度 | 结论 |
|---|---|
| IMPLEMENTATION_STATUS | PASS：原 3 项修复全部关闭，未发现新增必修项 |
| TEST_STATUS | PASS（已执行范围）；本轮核验并复用原始测试产物，没有重跑无变化测试 |
| PERFORMANCE_EVIDENCE_STATUS | PARTIAL：OPT-001 保持 OPTIONAL / DEFERRED，未新增吞吐结论 |
| RESOURCE_EVIDENCE_STATUS | Windows 真实跨卷目录查询 PASS；七牛 1 GiB/100 GiB NOT_VERIFIED |
| EXTERNAL_GATE_STATUS | BLOCKED_EXTERNAL：七牛目标环境；跨平台与实际卷挂载文件夹专项仍待实测 |
| RELEASE_STATUS | BLOCKED_EXTERNAL：真实七牛、浏览器 CORS、故障恢复和容量门禁尚未完成 |
| OPEN_ACTIONABLE | 0 |
| GOAL_STATUS / Next Action | 本轮 Review 完成；STOP，不进入自动 Fix |

## 问题状态

| 编号 | 状态 | 本轮依据 |
|---|---|---|
| FIX-001 初始化补偿 | CLOSED | 上次独立复审已确认提交结果重读、持久清理、竞争保护及两 ORM 的 Local 重启恢复；相关代码哈希未变化 |
| FIX-002 元数据预算 | CLOSED | 自定义 16 项与系统字段分开，保留前缀及总字节预算继续受限；旧格式会话恢复证据有效，相关代码未变化 |
| FIX-003 实际暂存卷查询 | CLOSED | 默认生产路径已改为实际目录查询，预检/监控/最终报告一致；真实 C: → H: 联接和子目录测试通过 |
| OPT-001 全量分片查询成本 | DEFERRED / OPTIONAL | 保留后续按分片定位或复用远端分页结果的优化；须维持回执 CAS、ETag 和强校验语义，本轮不扩大修复范围 |

## FIX-003 验收依据

### 实现与真实调用链

- [CapacityProbeDiskSpace.cs](H:/Bing_Framework/Bing.NetCore/samples/Bing.FileStorage.CapacityProbe/CapacityProbeDiskSpace.cs:63) 在 Windows 使用目录级 GetDiskFreeSpaceExW 查询当前调用者可用空间；卷标识通过 GetVolumePathNameW 解析，不再使用 Path.GetPathRoot 决定采样盘。
- Linux/macOS 分支使用 realpath 解析包括祖先目录在内的链接，再从系统挂载列表按最长路径边界匹配挂载点；空间查询使用实际目录。未将字符串根目录当作挂载点依据，原生分配在 finally 中释放。
- 目录不存在、不可访问或解析失败时抛出异常，不静默回退；空间使用 64 位值，预算乘法保持 checked。
- [Program.cs](H:/Bing_Framework/Bing.NetCore/samples/Bing.FileStorage.CapacityProbe/Program.cs:28) 的预检查调用默认 Measure，运行采样和 freeAfter 均调用同一快照的 ReadAvailableBytes。报告中的目录、卷、预算与查询来源相符，旧的按卷盘符构造 DriveInfo 路径已移除。
- 远端使用 Path.GetTempPath，与 [S3FileStorage.UploadPartAsync](H:/Bing_Framework/Bing.NetCore/framework/src/Bing.FileStorage.S3/S3FileStorage.cs:277) 的真实暂存路径一致；Local 选择 root。提前创建 root 支持首次运行，不改变 Provider 的路径安全规则。
- Local 的 3 倍长度加 10 GiB、远端分片大小乘实际并发预算保留。Provider/七牛门面程序集身份字段未改变，没有引入数据库或公开 API 改动。

### 验证覆盖

[CapacityProbeDiskSpaceTests.cs](H:/Bing_Framework/Bing.NetCore/framework/tests/Bing.FileStorage.Qiniu.Tests/CapacityProbeDiskSpaceTests.cs:136) 的真实联接用例没有注入解析器，直接使用默认生产实现：

1. 在 C: 系统临时目录建立指向 H: 专用空目录的联接。
2. 显式断言联接根及子目录都识别目标卷，且不等于源卷。
3. 对预检查 AvailableBytes 和后续 ReadAvailableBytes，分别与独立目录级 OS 查询比较。
4. 使用有限容差容纳两次查询间的系统磁盘变化；目标卷相等/源卷不等的断言仍是精确匹配。
5. 只清理本次创建的空目录和联接，不递归删除目标内容。

真实普通目录、缺失目录失败及原 4 项预算测试也通过。net6.0、net8.0 各 7 通过、0 失败、1 跳过；跳过的是当前 Windows 环境下的 Unix 专项。测试项目直接链接生产 helper，不存在另一套被测实现。

### 验证边界

Windows 实际卷挂载文件夹、Linux/macOS 的真实挂载行为尚未独立执行。代码已具备对应查询分支，现有证据只能证明 Windows 跨卷目录联接，不能将 Unix 门控跳过标为通过。这些外部验证缺口不构成已确认的代码缺陷，不因此重新打开 FIX-003。

本轮没有填满磁盘，也没有执行云端或 1/100 GiB 传输。

## 计划验收矩阵

| 阶段 | 结论 | 实现及剩余条件 |
|---|---|---|
| QN-01 协议测试及云端门控 | PARTIAL | SDK 本地协议验证和门控已交付；真实七牛兼容性仍 NOT_VERIFIABLE |
| QN-02 Provider、配置、映射、能力 | PASS（本地范围） | 已接入，现有源码/验证身份一致 |
| QN-03 回执、恢复、完成与直传 | PASS（本地范围） | FIX-001/002 关闭，两 ORM/Local/MinIO 证据有效；七牛实测独立门控 |
| QN-04 Admin、Demo、附件和工具 | PASS（本地范围） | 接入及本地业务闭环已验证；目标云端浏览器/CORS 待验收 |
| QN-05 文档、打包消费和容量 | PARTIAL | 本地交付及 FIX-003 验收通过；真实七牛容量和跨平台专项仍缺环境证据 |

## 本轮证据核验

影响范围仅为容量工具及其链接测试。没有相关源码变化或新失败，因此按 GOAL_RULES 复用既有验证，不执行全解决方案、业务全量、云端或大容量矩阵。

- 合并原始交付、fix-round-1 和 fix-round-2 清单，核对 54 个相关源码/测试/文档/证据脚本 SHA-256 全部一致。
- 逐一解析并核对 16 份 TRX：Round 1 的 14 份及 Round 2 的 2 份。Round 1 容量用例由 Round 2 证据替代，重叠范围不累加成总测试数。
- 独立子审查核对 Round 2 三个程序集哈希全部匹配：容量工具 net8.0、Qiniu 测试 net6.0/net8.0。
- 容量工具构建日志为 0 警告、0 错误。源码变化范围与执行报告一致，未发现以 Mock 替代实际 Windows 联接验收。
- 本轮相关 tracked 路径 git diff --check 通过；容量源码等未跟踪文件通过实际内容、修复前快照差异和哈希核对，未把空 git diff 当作无改动证明。

机器可读核验见 [source-evidence-check.json](evidence/re-review-after-round-2/source-evidence-check.json)，核验脚本见 [verify-scope.py](evidence/re-review-after-round-2/verify-scope.py)。本轮未覆盖或重写历史证据。

主要有效测试证据：

| 范围 | 结果来源 |
|---|---|
| 容量查询及预算 | fix-round-2/capacity-net6.0.trx、capacity-net8.0.trx：各 7 通过、1 跳过 |
| 初始化补偿 | fix-round-1/initialization-accepted：每 TFM 12 项；包含在业务全量中 |
| 文件管理业务 | fix-round-1/business-final：每 TFM 231 通过、6 跳过 |
| Local Provider | fix-round-1/local-accepted：每 TFM 29 通过 |
| MinIO Provider / 业务 HTTP | fix-round-1：每 TFM 分别 5 / 24 通过 |
| 两套 Admin | fix-round-1：各 24 通过、1 个环境门控跳过 |
| 旧元数据及 Local 重启独立探针 | 上次 re-review-20261026 证据继续有效，相关生产源码未变化 |

## 剩余事项分类

- Completed：FIX-001、FIX-002、FIX-003，共 3/3。
- Open Actionable：无。
- Blocked Approval：无。
- Blocked External / NOT_VERIFIED：七牛真实代理与 SignedParts、签名、故障恢复、跨日会话、浏览器 CORS、1 GiB/100 GiB；Linux/macOS 和 Windows 实际卷挂载专项。
- Accepted Limitations：数据库持续不可用或进程在 UploadId 持久化前硬退出时，仍需后端生命周期和人工对账；本轮不改变此前明确的跨系统恢复边界。
- Verified Boundaries：Windows 跨卷联接根/子目录查询；缺失目录明确拒绝；原空间预算行为。
- Not Applicable：本轮重新打包、数据库迁移、全量 Benchmark。
- Deferred：OPT-001 全量分片查询成本优化。

本轮审查完成后停止。保留现有工作区修改，未提交、推送、创建 PR、发送外部消息或执行真实业务迁移。
