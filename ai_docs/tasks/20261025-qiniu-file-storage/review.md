<!-- AI_REVIEW_STATUS: PASS_WITH_ISSUES -->
AI_TASK_ID: 20261025-qiniu-file-storage
AI_REVIEWED_AT: 2026-10-26T05:58:23Z

# 分片分页优化独立复审

## 结论

本轮未确认新增 MUST_FIX 或 SHOULD_FIX。此前 OPT-001 已真实接入代理回执主链路，可以关闭；剩余两项为可选性能改进，不构成当前实现错误。结论为 PASS_WITH_ISSUES。

审查覆盖完整七牛计划及其 PQ-01～03 增量、执行报告、当前暂存/工作区差异、实际源码、测试断言和原始产物。主审重点检查分片查询/回执状态机及相邻 HTTP、ORM 查询链；luna_worker 独立核对源码身份、TRX、包消费及关键断言。没有修改生产代码、测试、plan.md 或 execution.md。

|状态维度|结论|
|---|---|
|IMPLEMENTATION_STATUS|PASS：OPT-001 已关闭，无新增必修问题|
|TEST_STATUS|PASS（已执行范围）；身份匹配后复用原始测试证据|
|PERFORMANCE_EVIDENCE_STATUS|PASS（请求/返回条数）；不代表网络延迟或吞吐基准|
|RESOURCE_EVIDENCE_STATUS|本轮不重跑容量；七牛 1 GiB/100 GiB 未验证|
|EXTERNAL_GATE_STATUS / RELEASE_STATUS|BLOCKED_EXTERNAL：七牛目标空间、真实浏览器/CORS、故障恢复和容量门禁仍待验收|
|OPEN_ACTIONABLE|0；两个可选优化记为 DEFERRED|
|Next Action / GOAL_STATUS|STOP / STOPPED_BLOCKED（外部门禁独立保留，不自动修复）|

前一版报告归档到 [review-before.md](evidence/re-review-pagination/review-before.md)。本轮证据复核见 [notes.md](evidence/re-review-pagination/notes.md)。

## OPT-001 验收

1. [MultipartFileStorageExtensions.cs](H:/Bing_Framework/Bing.NetCore/framework/src/Bing.FileStorage.Abstractions/MultipartFileStorageExtensions.cs:3) 新增独立可选接口，未向现有 IMultipartFileStorage 增加必实现成员。扩展验证序号/取消，优先点查，旧后端回退到唯一序号匹配；没有缓存远端回执。
2. [FileStorageRegistration.cs](H:/Bing_Framework/Bing.NetCore/framework/src/Bing.FileStorage/FileStorageRegistration.cs:100) 工厂入口仍验证容器；包装器经扩展向底层派发，旧 Provider 不会因缺少新接口而报不支持。文档明确类型判断不等于底层具备加速能力。
3. [S3FileStorage.cs](H:/Bing_Framework/Bing.NetCore/framework/src/Bing.FileStorage.S3/S3FileStorage.cs:367) 以 number-1 为排他游标、MaxParts=1 读取目标位置，并精确比对返回序号；后继片不能代替缺失的目标。公共页读取校验数量、顺序、长度/ETag 和续页标记，错误及取消没有转换成 null。
4. [FileManagementService.cs](H:/Bing_Framework/Bing.NetCore/components/src/Bing.Biz.FileManagement/FileManagementService.cs:205) 每次 CAS 重试仍重新读取拥有者/范围/状态/期限和远端目标片，核对上传句柄、长度、摘要及 ETag；仅替换原全量枚举调用。未发现冻结/中止后写入的新路径。
5. GetSessionAsync、ConfirmPartsAsync、完成任务与 Provider 独立完成入口继续读取/校验完整清单；没有用点查结果代替整文件完成条件。完整枚举现在直接使用验证后的升序结果，避免另行排序。

6400 片元数据测试确认：全量枚举为 7 次 SDK 请求、6400 条属性，尾片查询为 1 次请求、1 条属性。两 ORM SQLite 测试实际上传尾片 3 字节，确认主链路用点查、重启恢复仍读取 7 页，并拒绝无其余可信回执的完成操作。该证据不含 100 GiB 实际传输。

## 可选优化

### OPT-002：状态轮询与完整分片清单分离

- 严重程度：LOW；处理要求：OPTIONAL；状态：DEFERRED；建议顺序：第一。
- 证据：[uploader.js:62](H:/Bing_Framework/Bing.NetCore/components/src/Bing.Biz.FileManagement.AspNetCore/Console/uploader.js:62) 的 waitForState 每秒调用会话详情；[FileManagementService.cs:160](H:/Bing_Framework/Bing.NetCore/components/src/Bing.Biz.FileManagement/FileManagementService.cs:160) 在 Completing、Quarantined 等状态仍读取全部数据库分片并返回已确认记录。客户端此时只使用 state，未使用 parts。
- 影响：在 6400 片的大文件合并或扫描期间，每次轮询仍需读取、物化、序列化和传输最多 6400 条分片记录；多个会话同时等待时成本放大。这是现有调用链的确定开销，本轮没有测量其延迟或带宽。
- 方案：增加仅状态查询，或兼容式 includeParts=false 参数，默认保持现有详情协议；waitForState 使用轻量结果，真正开始/恢复上传时再读取一次完整清单。可在长时间等待时逐步放慢轮询并保持暂停/取消响应。
- 约束：保留会话所有者、租户和终态判断；恢复仍以远端与可信回执核对后的完整清单为准，不能把轻量结果中的空 parts 当作无已上传分片。
- 验证：两 ORM 的 6400 片 Completing/Quarantined 状态轮询断言不调用 ListPartsAsync，响应大小不随片数增长；浏览器覆盖待初始化→上传、完成等待、暂停与刷新恢复。
- 推迟原因：这是本次已批准“单片回执查询”之外的 HTTP/客户端改进；当前协议仍正确，不阻塞本轮代码交付。建议后续任务：upload-session-status-query。

### OPT-003：文件列表授权批量加载，减少逐条查询

- 严重程度：LOW；处理要求：OPTIONAL；状态：DEFERRED；建议顺序：第二。
- 证据：[FileManagementService.Queries.cs:87](H:/Bing_Framework/Bing.NetCore/components/src/Bing.Biz.FileManagement/FileManagementService.Queries.cs:87) 的 SearchAsync 和 [FileManagementService.cs:315](H:/Bing_Framework/Bing.NetCore/components/src/Bing.Biz.FileManagement/FileManagementService.cs:315) 的 ListAsync 对每条候选分别调用 AuthorizeReadAsync。它会重新 FindAsync 加载文件聚合；[EF 实现:22](H:/Bing_Framework/Bing.NetCore/components/src/Bing.Biz.FileManagement.EntityFrameworkCore/EfFileManagementStore.cs:22)、[FreeSql 实现:478](H:/Bing_Framework/Bing.NetCore/components/src/Bing.Biz.FileManagement.FreeSql/FreeSqlFileManagementStore.cs:478) 分别查询 File、Session、Bindings。
- 影响：已有游标/候选分页限制了单批规模，但每条候选仍有额外聚合查询和事务。对于 50 条存在且可见的候选，按当前代码路径可推导约 150 次额外聚合 SELECT，尚未计入具体业务授权查询；这不是实测耗时结论。低授权命中率下需要扫描更多候选，开销进一步增加。
- 方案：每批候选批量加载当前文件状态与关联，再在内存中执行原读取授权规则；列表授权不需要的 Session 不加载。复用同一授权判定逻辑，保留旧偏移接口，同时继续优先使用已有 search 游标。
- 约束：不得绕过业务处理器、跨租户隔离或 Ready 过滤；不能为了减少查询让被拒绝文件占用页容量；不能引入跨请求权限缓存。数据库读取保持有界且有取消支持。
- 验证：EF/FreeSQL 真实 SQLite 的 SQL 计数随批次数而非候选数增长；原跨租户、未注册处理器拒绝、混合可见性、并发删除、授权过滤后分页及游标回归保持通过。
- 推迟原因：需要新的批量仓储契约及两 ORM 实现，超出本轮点查优化；当前返回与授权语义未发现错误。建议后续任务：file-list-batch-authorization。

不建议直接删除完成链路的多层核验或用缓存复用远端 ETag 来继续压缩请求。当前这些核验承担延迟分片、后端独立使用及完成对账语义，任何后续合并都须先证明等价性。

## 计划验收矩阵

|阶段|结果|依据/边界|
|---|---|---|
|QN-01 协议及云端门控|PARTIAL|本地与 MinIO 证据存在，七牛云端未执行|
|QN-02 配置、映射、能力|PASS（本地）|配置身份、模式与能力没有被点查改动绕过|
|QN-03 回执、恢复与完成|PASS（本地）|点查主调用链与根 CAS 安全条件仍成立|
|QN-04 Admin、附件与工具|PASS（本地）|沿用实际宿主/业务测试，未变更接入协议|
|QN-05 包消费与容量|PARTIAL|四包与 net6.0 消费身份匹配；七牛大容量外部验证未完成|
|PQ-01 可选能力与兼容|PASS|旧 Provider 回退、容器、取消、参数及消费者覆盖|
|PQ-02 S3 点查与业务接入|PASS|首尾/稀疏序号、异常页、CAS 重查与两 ORM 证据一致|
|PQ-03 成本及验证|PASS（本地计划范围）|6400 片请求数对比、net6/net8、真实 MinIO、两 Admin 与包消费；未冒充云端验收|

## 验证与证据复用

- Candidate 基线 HEAD：87b68e7dadbffb2f1de58d1ea7013e678bc83888；使用当前未提交工作区，不只依据 Git tracked diff；暂存区为空。
- 独立复算 16 个记录文件 SHA-256，全部一致；解析 19 份显式 TRX，与 test-summary.json 的通过/跳过计数及明细一致。四个 nupkg 内 DLL 和 net6.0 consumer DLL 身份一致。
- 审读了可选能力、旧后端包装回退、目标片首尾/空洞、异常页、CAS 冲突远端变化、重启与冻结等测试断言，没有将接口存在当作集成完成。
- 证据包括 net6/net8 各 35 项 Core/Local、16 项 S3 离线、29 项 Qiniu、233 项业务；真实 MinIO 每框架 5 项 Provider/24 项业务与 HTTP；两 Admin 分别 185/183 通过。门控跳过按原始报告单列，定向测试和全套不重复累计。
- 本轮相关源码没有再变更或新增失败，按 GOAL_RULES 复用这些测试，不重跑全解决方案、容器或容量矩阵。L0 本轮读取/身份与 scoped diff-check 通过；L1/L2/L3 复用未变化范围；L4/L5 不适用。
- 对两项可选建议只做调用链分析，没有增加未经测量的吞吐提升百分比，也没有伪造新的数据库成本 Benchmark。

## 汇总与边界

- Completed：原 FIX-001/002/003 继续 CLOSED；OPT-001 本轮 CLOSED。
- Open Actionable：无；Blocked Approval：无。
- Deferred：OPT-002 轻量状态轮询、OPT-003 列表批量授权读取。
- Blocked External：七牛真实代理/直传、游标兼容、跨日续传、签名/CORS、故障恢复和 1 GiB/100 GiB；既有跨平台容量专项也保持此前边界。
- Accepted Limitations：跨系统 UploadId 持久化前硬退出及数据库长期不可用仍需后端生命周期/人工对账，未被本轮优化改变。
- No-Progress Check：CHANGED（新点查实现有完整证据，旧性能项关闭）；Next Action：STOP。本轮只审查，不自动启动 Fix。

仅更新 review.md 和 re-review-pagination 证据目录。保留其他工作区修改，未 commit、push、创建 PR、发送外部通知或执行真实业务迁移。
