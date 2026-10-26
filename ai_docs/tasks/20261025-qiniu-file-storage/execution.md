<!-- AI_EXECUTION_STATUS: COMPLETED -->
AI_TASK_ID: 20261025-qiniu-file-storage
AI_EXECUTION_FINISHED_AT: 2026-10-26T01:59:51.983217+00:00

# 七牛云文件存储实施执行报告

## 执行结论

本地代码、测试、宿主和工具接入、部署文档、Release 打包及 net6.0 消费已完成。七牛默认为 ServerProxy，SignedParts 需目标空间实际验收后显式启用。基础契约仍为 netstandard2.0，Provider 为 net6.0，测试覆盖 net6.0/net8.0。

云端兼容性及容量验收未完成：未提供七牛测试账号、区域和空间。本报告不将 SDK 替身或 MinIO 的通过结果当作七牛云端证据。

## 任务信息与计划执行

- Task ID：20261025-qiniu-file-storage；执行器 Codex；开始时间 2026-10-25T14:20:37.808Z。
- 执行依据：[plan.md](plan.md)、execute-plan、项目 AGENTS 与用户 GOAL_RULES。
- 主代理负责 S3 契约、业务回执、状态机和整体验证；luna_worker 负责明确边界的宿主/工具/文档、Admin 测试与包消费。

| 阶段 | 本地交付 | 验收边界 |
|---|---|---|
| QN-01 | 新测试项目、真实 SDK HTTP 请求观察、七牛环境门控 | 离线协议通过；云端 3 个用例每 TFM 跳过 |
| QN-02 | 七牛包、配置快照、桶映射、模式与能力声明 | 新包构建、Provider 测试与真实 MinIO 回归通过 |
| QN-03 | 代理回执、CAS、恢复、完成收尾、直传保持强校验 | 两 ORM SQLite 的 20 个代理场景通过，且包含在业务全量验证中 |
| QN-04 | 两 Admin、Demo、Tool、CapacityProbe、用户资料附件 | 消费者构建通过；HTTP 与真实用户资料业务的 Local/代理闭环通过 |
| QN-05 | 部署说明、测试映射、四包 Release、net6.0 包消费 | 本地通过；真实七牛及 1/100 GiB 门禁单列 |

## 已完成事项

1. `AddBingQiniuStorage` 校验对应区域的 HTTPS S3 域名、显式凭据及空间映射，注册后深拷贝。所有 S3 读、写、Range、属性、删除、分片与签名都通过同一个逻辑容器→实际桶解析入口，对象键保留逻辑容器和租户范围。
2. 通用 S3/MinIO 保持 SignedParts 默认；七牛默认 ServerProxy。新增 ServerVerifiedParts 与 RequiresCompletionCleanup，Local 显式声明完成后的清单收尾。已有无模式元数据的 S3 分片会话按旧 SignedParts 兼容，有模式元数据的会话必须一致。
3. 代理实际分片先落单片临时文件、计算 SHA-256，核验预登记摘要后发送普通 SigV4 载荷请求；禁用可选 SDK flexible checksum，保留载荷签名，不依赖远端返回扩展分片摘要。SaveAsync 和迁移复用同一实现。
4. 回执保存重新查询会话状态、期限和远端分片版本，使用根 File.Version CAS；竞争重试再次核对 ETag。缺回执、远端变化、冻结/中止/过期均不能误认为有效完成。同摘要重登记只续期，不清空 ETag。
5. 完成及恢复继续使用持久任务、唯一位置和会话元数据对账。代理对象已完成后不再误走 Local 清单收尾；Local 仍执行幂等收尾。
6. 两 Admin、Demo、迁移 Tool 和容量工具增加 Qiniu 配置入口。用户资料授权、上传→绑定→内容读取→解绑→删除复用原应用服务，不建立第二套业务状态。
7. 边界测试实际发现并修复不可 Seek 空流触发 AWS SDK 分块签名的问题：验证输入确实为空后，向 SDK 提供 Stream.Null，原流仍由调用方拥有。真实 SDK 请求测试与 MinIO 空文件回归覆盖此修复。

## API、数据与配置变化

- 新包 Bing.FileStorage.Qiniu；增加 BingQiniuStorageOptions、S3UploadMode、BingS3StorageOptions.ContainerBuckets / UploadMode 和两个能力位。
- 新增 `S3FileStorageBackend(name, options, client)` 重载，支持调用方拥有的 SDK 客户端及不可变选项；旧构造函数保留。传入客户端的生命周期和 SDK 传输设置仍由调用方负责。
- 没有新数据库字段、表或生产 ORM 变更。复用 FileUploadPart.Sha256 / ETag、File.Version，以及现有 BackendMetadataJson 持久化模式。
- 未映射的旧 S3 实例仍使用逻辑容器同名桶。配置了映射的实例只允许显式映射的容器。改变区域、映射或模式必须使用新 StoreName；旧实例保留以读取历史文件。
- 未声称原子 CreateOnly 能力；S3 继续明确拒绝此写入模式。

## 修改文件

| 范围 | 文件 |
|---|---|
| 契约与 Provider | framework/src/Bing.FileStorage.Abstractions/StorageModels.cs；Bing.FileStorage.FileSystem/FileSystemStorage.cs；Bing.FileStorage.S3/{BingS3StorageOptions,S3FileStorageBackend,S3FileStorage}.cs；新增 Bing.FileStorage.Qiniu/{Bing.FileStorage.Qiniu.csproj,BingQiniuStorageExtensions.cs,README.md} |
| 业务 | components/src/Bing.Biz.FileManagement/{FileManagementService,FileOperationProcessor,FileModels}.cs |
| 测试 | framework/tests/Bing.FileStorage.Qiniu.Tests/；components/tests/Bing.Biz.FileManagement.Tests/{Bing.Biz.FileManagement.Tests.csproj,ProxyUploadTests.cs,FileHttpTests.cs}；两 Admin 测试 csproj；Shared/AdminQiniuStorageRegistrationTests.cs、AdminUserInfoFileAuthorizationTests.cs |
| 消费者 | 两 Admin csproj 和共享 FileStorageModule.cs；Demo/CapacityProbe/Tool 的 csproj、Program.cs；Tool/appsettings.example.json |
| 工程与文档 | Bing.All.slnx；docs/modules/file-storage.md；docs/operations/file-management/qiniu.md、qiniu.example.json；本任务 plan/execution/test-map/evidence |

## 测试结果

所有下列最终验收运行均为 0 失败。每 TFM 的结果单独保存在 evidence 中，计数不跨重复执行累加。

| 范围 | net6.0 | net8.0 | 证据前缀 / 说明 |
|---|---:|---:|---|
| Qiniu Provider + SDK 协议 | 13 通过 / 3 跳过 | 13 通过 / 3 跳过 | qiniu-accepted；跳过真实七牛 |
| 文件业务全项目 | 219 通过 / 6 跳过 | 219 通过 / 6 跳过 | business-final；初次未启用 MinIO/ClamAV |
| Local Provider | 22 通过 | 22 通过 | Bing.FileStorage.Tests |
| ASP.NET Core 存储适配 | 2 通过 | 2 通过 | Bing.FileStorage.AspNetCore.Tests |
| FreeSql 独立仓储契约 | 7 通过 | 7 通过 | Bing.Biz.FileManagement.FreeSql.Tests |
| S3 首轮离线 | 2 通过 / 3 跳过 | 2 通过 / 3 跳过 | Bing.FileStorage.S3.Tests；云端项后续启用 MinIO 补跑 |
| 真实 MinIO Provider 专项 | 4 通过 | 4 通过 | minio-provider（含已有 MinIO 配置契约） |
| 真实 MinIO 业务 + 最终 HTTP 关联链路 | 24 通过 | 24 通过 | minio-business-http；包含两 ORM、Local/代理 HTTP |
| 最后空流修复的 MinIO 影响回归 | Provider 1 + 业务 10 通过 | Provider 1 + 业务 10 通过 | minio-provider-empty、minio-business-http-empty |

两套 Admin 使用其现有 net6.0 测试目标：

- 宿主/注册过滤：EF Core 9 通过、3 个 MySQL 门控跳过；FreeSql 同样 9/3。见 [host-validation.md](evidence/host-validation.md) 及 admin-host-registration/*.trx。
- 用户资料授权与控制器过滤：各 20 通过、1 个 MySQL 门控跳过。包含真实两 ORM 用户资料和非空代理附件，不连接真实七牛。见 evidence/admin-profile/。
- 最终生产符号追溯详见 [test-map.md](test-map.md)。

本地 MinIO 复用此前任务专用停止容器 bing-files-fix1-minio，固定镜像 `minio/minio@sha256:14cea493d9a34af32f524e538b8346cf79f3321eff8e708c1e2960462bd8936e`，仅监听 127.0.0.1:19020，目录 H:/temp/bing-files-fix1-minio。凭据只读入测试进程环境，没有输出或保存到证据；两次验证均在 finally 恢复停止状态。可复现脚本 [run-minio-regression.ps1](evidence/run-minio-regression.ps1)，最后空文件影响回归使用 `-EmptyOnly`。

早期 qiniu-wire TRX 保留了一次失败：非 Seek 空流实际被 SDK 编码为分块载荷。该问题由最终 SaveEmptyAsync 修复解决，最终以 qiniu-accepted 和 minio-*-empty 为准。qiniu-net8、qiniu-final、proxy-net8 为开发阶段证据，不重复计数。

## Build / 打包 / 格式检查

- 新 Qiniu Provider 构建通过；两 Admin、Demo、CapacityProbe、Tool 构建均 0 错误。
- Abstractions、Core、S3、Qiniu 以仓库现有版本 7.0.0 生成 Release nupkg/snupkg，未发布。net6.0 PackageReference consumer 编译、运行、默认能力解析通过；最终包及消费 DLL 校验记录见 [包消费报告](evidence/package-consumption/README.md)。
- 存在已有 Admin Microsoft.Extensions 10.0.0 / net6.0 兼容提示、XML 注释/隐藏警告；测试中现有 ISystemClock 弃用提示；Admin 链接 SDK 测试辅助类有 CS8632 nullable 上下文提示；临时 net6.0 consumer 有 NETSDK1138 生命周期提示。均未改变本任务运行结果。
- 本任务改动的 tracked 文件 `git diff --check` 通过；全部工作区检查发现其他任务已有尾空白（3 份 20261021 计划及 EfCoreSqliteUnitOfWorkTestSupport.cs），未改动这些文件。另见 evidence/source-check.json 对本任务文件的 UTF-8、XML/JSON 与尾空白验证。

## 部分 / 未完成事项与门禁

| 门禁 | 状态 | 所需后续条件 |
|---|---|---|
| 真实七牛代理 / SignedParts / 签名过期 / 篡改载荷拒绝 | BLOCKED_EXTERNAL | 专用空间、Endpoint、Region、AK/SK；直传测试额外 SIGNED_TESTS=1 |
| 七牛实际应用重启续传、完成响应丢失、跨日 UploadId 期限 | NOT_VERIFIED 云端 | 在目标部署执行故障演练；本地 SQLite+Provider 故障注入已经通过 |
| 七牛浏览器预检 / CORS / 签名刷新 | NOT_VERIFIED 云端 | 实际前端 Origin、浏览器与目标空间；不能用 HttpClient 代替 |
| 七牛 1 GiB / 100 GiB | NOT_VERIFIED | CapacityProbe 已接通，未运行付费云端容量测试，须记录内存/磁盘/吞吐/取消与清理 |
| Admin MySQL 与真实 ClamAV 额外门控 | 本轮未运行 | 本轮不改 ORM Schema 或扫描实现，复用已有能力，仅验证受影响 SQLite 链路 |

默认保持代理模式；无真实七牛证据时不宣称目标空间可直接生产投用。部署、临时磁盘容量、CORS、会话期限及迁移操作见 [七牛部署说明](../../../docs/operations/file-management/qiniu.md)。

## 计划偏差 / 基线问题 / 已知问题

- 用户批准先本地实现、云端后验证，因此云端与容量缺环境保持独立门禁，不以此触发自动修复循环。
- 新增可注入 SDK 客户端的 S3 构造重载，用于统一生产适配和本地协议/业务故障注入，未引入第二个 Provider 实现。
- 新项目首次 `--no-restore` 因缺 assets 失败，正常 restore 后构建通过。空文件签名缺陷已修复，无未解决的本轮代码失败。
- OPEN_ACTIONABLE=0；本地 Implementation/Tests=PASS；七牛 Release/Capacity=BLOCKED_EXTERNAL 或 NOT_VERIFIED。未知云端行为不进行猜测性降级。

## 风险、Reviewer 注意事项与 Git 状态

重点关注回执 CAS 与远端 ETag 的一致性、冻结后回写禁止、原有 SignedParts 强校验、Local 收尾标志及 StoreName 不可变配置。代理模式每片使用临时磁盘，并在回执提交前核对远端分片清单；目标容量/吞吐尚须实测。短期 S3 下载链接在过期前不受业务解绑即时撤销，逐次授权应使用应用下载入口。

保留执行前已有的大量未提交/未跟踪文件存储与其他任务修改。未自动 git commit、未自动 git push、未自动创建 PR；未执行真实业务迁移、未修改云端空间/CORS/权限。任务结束使用 task-finish 的 --no-notify，不发送外部消息。

## Review 修复记录

### Round 1

- Review 状态：NEEDS_FIX；Fix Scope：recommended；模式：REVIEW_FIX；执行器：Codex。
- Review 文件：`ai_docs/tasks/20261025-qiniu-file-storage/review.md`，保留独立审查内容，未修改其状态或 Finding。
- 本轮范围为 FIX-001、FIX-002、FIX-003。OPT-001 远端分片定点查询为 OPTIONAL，继续 DEFERRED。
- 主代理负责补偿边界、公共元数据契约与最终验证；luna_worker 负责容量探针、独立 Local/MinIO 元数据测试及 Release 包消费验证。
- Change Impact：Initialize/Abort/任务恢复、受信会话重建、S3 创建及空对象元数据、容量探针。先运行直接故障/边界测试，再验证两 ORM 业务、Local、真实 MinIO、两 Admin 和消费者；未运行全解决方案或大容量探针。

#### FIX-001

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改文件：`FileOperationProcessor.cs`、新增 `FileOperationProcessor.Initialization.cs`、`FileOperationAdministration.cs`；新增 `InitializationCompensationTests.cs`；SDK 测试辅助类增加 Abort 故障注入。
- 根因：发布 UploadId 的数据库方法抛异常时没有补偿，返回 false 的路径也只尝试一次中止，没有持久恢复依据。
- 修复：发布异常或 CAS 失败后，以独立 10 秒恢复令牌查询当前记录。若同一 UploadId 已提交则保留会话；否则使用最新根版本 CAS 插入包含完整原位置、UploadId 和恢复元数据的 Abort 任务，再尝试后端中止。插入先于中止，根 CAS 同时阻止旧版本迟到发布。保存补偿任务时最多重试 3 次，并按固定任务 ID 吸收提交响应丢失，不重复登记。后端中止失败保留任务，重启后执行；达到重试上限可通过现有管理入口创建后继任务。原始数据库异常仍进入初始化任务错误记录。补偿快照保留原存储，调度资源限制覆盖它，不随文件后续迁移改变清理目标。
- 数据库：复用现有 FileOperation.Payload 和根 CAS，没有新增表、字段或 ORM 实现差异。
- 验证：`InitializationCompensationTests` 每 TFM 12 项通过，覆盖真实 EF Core / FreeSql SQLite 的提交前失败、提交后丢响应、补偿任务提交丢响应、取消、Abort 失败重启恢复和初始化竞争；已提交/胜出会话继续完成实际上传。见 `evidence/fix-round-1/initialization-accepted*.trx`。
- 边界：数据库持续不可用时无法判断提交结果，也不能写补偿记录；此时保留带 FileId/StoreName/已知 UploadId 的错误日志并抛出聚合异常，不盲目 Abort。没有取得 UploadId 的后端未知结果、获取标识后到持久化前进程直接崩溃，仍需部署方核实后端未完成分片回收策略并做人工对账；该跨系统窗口已写入部署指南，未声称分布式原子提交。

#### FIX-002

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改文件：`StorageModels.cs`、`S3FileStorage.cs`、`FileModels.cs`、S3 README；新增 `MetadataBudgetTests.cs`、`FileWriteMetadataCapacityTests.cs`、`MinioMetadataCapacityTests.cs`。
- 根因：Provider 追加 token/mode 后重新使用普通构造函数，导致两个内部字段占用调用方 16 项元数据额度。
- 修复：普通 FileWriteRequest 构造保留 16 项限制；新增明确用于受信分片持久化恢复的 RestoreMultipart，将 bing-upload- 内部字段计入独立的有界额度。S3 新建对象/分片仍拒绝调用方保留字段，内部字段通过原 BackendMetadataJson 完整持久化与恢复。空对象、普通 SaveAsync、分片创建/恢复统一检查 S3 元数据键和值的 2 KiB 总额度。
- 验证：14/15/16 项用户元数据在 ServerProxy 和 SignedParts 下的空/非空保存、分片恢复与完成通过；17 项用户元数据、保留字段伪造和总字节超限在 SDK 调用前拒绝。Local 新增 7 项真实文件 IO 测试、MinIO 新增 1 项实际服务测试（空/非空对象）均通过。
- 兼容：基础包继续 netstandard2.0，Provider 继续 net6.0；原构造函数签名未变。新静态恢复入口已通过 net6.0 PackageReference consumer 验证。

#### FIX-003

- 严重程度：MEDIUM；处理要求：SHOULD_FIX；执行状态：COMPLETED。
- 修改文件：容量 Probe 的 Program、README，新增 `CapacityProbeDiskSpace.cs`；新增 `CapacityProbeDiskSpaceTests.cs` 并在 Qiniu 测试项目链接 helper；更新七牛指南。
- 根因：不区分后端，始终检查/采样 --root 所在卷，而远端分片实际使用系统临时目录。
- 修复：Local 保留 `3 × 文件大小 + 10 GiB` 预算；MinIO/Qiniu 使用 Path.GetTempPath() 实际卷，按分片大小乘真实并发预算。当前探针是顺序上传，预算为 1 个 16 MiB 分片。输出记录 root、采样目录、采样卷、预算、S3 执行程序集及七牛门面程序集身份。上传过程的最低空闲空间、前后空间数值都来自该实际卷。
- 验证：4 项可注入容量查询测试每 TFM 通过，覆盖不同卷的 root 不足/temp 充足、root 充足/temp 不足、远端并发预算和 Local 原预算；容量工具实际构建通过。没有实际填满磁盘，没有运行 1/100 GiB。

### Round 1 验证结果

下表均为最终验收结果；不同过滤范围可能重叠，不累加为总测试数。TRX 和日志位于 `evidence/fix-round-1/`，另有机器可读的 `validation-summary.json`。

| 范围 | net6.0 | net8.0 | 说明 |
|---|---:|---:|---|
| 初始化补偿专项 | 12 通过 | 12 通过 | 包含两 ORM SQLite，已计入业务全量 |
| Qiniu Provider / 协议 / 元数据 / 磁盘预检 | 27 通过，3 跳过 | 27 通过，3 跳过 | 跳过真实七牛 |
| 文件管理业务全项目 | 231 通过，6 跳过 | 231 通过，6 跳过 | MinIO 另行启用门控；ClamAV 未运行 |
| Local Provider | 29 通过 | 29 通过 | 真实文件系统 |
| 实际 MinIO Provider | 5 通过 | 5 通过 | 固定隔离镜像，包含 16 项元数据 |
| 实际 MinIO 业务 + HTTP | 24 通过 | 24 通过 | 两 ORM、Local/代理 HTTP 闭环 |
| Admin EF Core / 用户资料、文件控制器、七牛注册 | 24 通过，1 跳过 | 不适用 | 现有测试目标 net6.0，MySQL 门控跳过 |
| Admin FreeSql / 同上 | 24 通过，1 跳过 | 不适用 | 现有测试目标 net6.0，MySQL 门控跳过 |

- 两 Admin 通过测试项目实际编译；Demo、CapacityProbe、Tool 的构建均为 0 错误。消费者脚本为 `validate-consumers.ps1`，可用 `-BuildOnly` 只重跑三个工具构建。
- 四个包 Abstractions/Core/S3/Qiniu 的 7.0.0 Release 产物重新打包；独立缓存的 net6.0 consumer 编译运行成功，验证默认代理和 16+2 元数据恢复，并将消费 DLL 与 nupkg 哈希逐一比对。详见 [本轮包消费报告](evidence/fix-round-1/package-consumption/README.md)。未发布包。
- 本轮实际 MinIO 使用原专用容器及固定摘要，脚本位于 `evidence/fix-round-1/run-minio-regression.ps1`；结束后确认恢复停止状态。凭据只进入进程环境，没有输出或写入证据。
- 21 个本轮源码/测试/文档文件的 UTF-8、尾空白及适用 XML 检查通过，SHA-256 保存在 `source-check.json`；相关 tracked 路径 git diff --check 通过。生产符号映射已更新到 test-map.md。
- 首次竞争测试的注入钩子在补偿登记时重复制造新胜出者，导致测试自身失败；改为只注入一次真实根 CAS 竞争后通过，未放宽生产断言。首次 Local net8.0 全量暴露原测试 `free+1` 受并行临时文件释放影响而不再超限，现改用“卷总容量+1”的声明长度，保持容量不足和不得发布对象的断言。首次 Demo 命令使用了错误项目目录名，修正为现有 Bing.FileManagement.Demo 后构建通过；失败日志保留。
- 已有 ISystemClock 弃用、Admin 依赖/nullable 上下文及 consumer NETSDK1138 提示仍在；本轮测试新增的 xUnit 断言风格提示已消除。没有未解决的本轮构建/测试错误。

### Round 1 汇总

- MUST_FIX：0；纳入 SHOULD_FIX：3；已完成：3；PARTIAL/BLOCKED/FAILED 修复项：0。
- IMPLEMENTATION_STATUS：本轮修复 COMPLETED；TEST_STATUS：本地及隔离 MinIO PASS；PERFORMANCE_EVIDENCE_STATUS：沿用此前边界，未做业务吞吐 Benchmark。
- RESOURCE_EVIDENCE_STATUS：磁盘预检修复已验证；真实七牛 1/100 GiB 仍 NOT_VERIFIED。
- EXTERNAL_GATE_STATUS：七牛云端兼容、跨日会话、浏览器 CORS 等仍 BLOCKED_EXTERNAL/NOT_VERIFIED；不以离线测试或 MinIO 结果替代。
- OPEN_ACTIONABLE（执行器本轮范围）：0；OPT-001：DEFERRED；No-Progress Check：CHANGED；Next Action：STOP，交独立 Reviewer 复验。
- review.md 仍保留 NEEDS_FIX 原审查证据，Executor 完成不代表 Reviewer 已给出 PASS。已完成本轮范围后不自动启动下一轮 Review/Fix。
- 未 commit、push、PR、通知或真实业务迁移；保留原工作区无关修改。收口执行 `task-finish --no-notify`。

### Round 2：实际暂存文件系统查询

- Task：20261025-qiniu-file-storage；模式 REVIEW_FIX；Review Round 2；Fix Scope recommended。
- 审查依据：review.md 的 FIX-003（MEDIUM / SHOULD_FIX）；FIX-001/002 已由独立审查关闭，本轮不重做；OPT-001 仍 DEFERRED。
- 执行状态：FIX-003 COMPLETED（执行器结论，独立 review.md 保持不变）。
- 根因：Path.GetPathRoot 只提取路径前缀，Windows 目录联接或卷挂载目录下该前缀不代表承载数据的卷；此前可注入测试绕过默认解析，没能揭示此问题。
- 修复：Windows 使用 GetVolumePathNameW 跟随联接/挂载点，使用 GetDiskFreeSpaceExW 对实际目录查询当前用户可用空间。Linux/macOS 使用 realpath 解析祖先链接、系统挂载表最长目录边界匹配，并通过目录级 DriveInfo 查询空间。无法解析、目录缺失或不可访问时明确失败，不退回路径根目录。
- 接入：CapacityDiskSnapshot.ReadAvailableBytes 让 Program 运行中和结束时继续按同一目录查询；预检查使用同一实现。首次使用提前创建 --root。Local 的 3 倍文件长度加 10 GiB、远端分片大小乘实际并发预算保持不变，程序集身份字段保持不变。
- 修改文件：samples/Bing.FileStorage.CapacityProbe/{CapacityProbeDiskSpace.cs,Program.cs,README.md}；framework/tests/Bing.FileStorage.Qiniu.Tests/CapacityProbeDiskSpaceTests.cs；docs/operations/file-management/qiniu.md；test-map.md 和本报告。
- 委派：luna_worker 独立补充预算回归、真实 Windows 联接、普通/缺失目录及 Unix 门控测试；主代理负责生产代码、调用链、证据和统一验证。
- 影响分析见 [impact.md](evidence/fix-round-2/impact.md)。没有公共 API、Provider、数据库、包或 TFM 变化，不重新打包或运行无关业务回归。

### Round 2 验证结果

| 验证 | 结果 | 证据 |
|---|---|---|
| L0：容量工具实际 build | 0 警告，0 错误 | evidence/fix-round-2/build.log |
| L1/L3：net6.0 容量专项 | 7 通过，0 失败，1 跳过 | capacity-net6.0.trx / log |
| L1/L3：net8.0 容量专项 | 7 通过，0 失败，1 跳过 | capacity-net8.0.trx / log |
| 真实目录行为 | C: 临时目录联接 → H: 专用空目录，根/子目录均命中目标卷；预检及后续采样对照独立目录级 OS 查询 | DefaultResolution_UsesJunctionTargetVolumeForPrecheckAndSubdirectorySampling |
| 原预算及异常 | 4 项原预算测试、实际普通目录、缺失目录明确拒绝通过 | CapacityProbeDiskSpaceTests |
| Unix | Linux/macOS 符号链接用例因 Windows 门控跳过；未宣称跨系统实测通过 | TRX 的 NotExecuted 项 |
| L2/L4/L5 | 无关 Provider/业务全量、全解决方案、大容量矩阵不重跑 | 工具内部查询变化不影响原业务证据 |

所有新日志、TRX、复现脚本、变更前快照和最终源码身份在 [fix-round-2](evidence/fix-round-2/)。本轮没有填满磁盘，没有运行 1/100 GiB 或真实七牛传输。Windows 实际卷挂载文件夹与 Linux/macOS 真实挂载环境未单独执行；Windows 采用明确支持联接链及卷挂载点的系统 API，实际跨卷联接已执行验证。环境探查时 Docker API 返回 500，未修改或重启外部容器环境。

Round 1 的“实际卷采样已验证”描述由本轮更正：旧测试只证明了选择 temp/root 和预算算法，未证明默认解析能跟随目录联接。旧证据保留，本轮替换受影响查询结论；review.md 的独立记录保持原样，下一步可由 Reviewer 复验。

### Round 2 汇总与分类

- Implementation：本轮 1/1 执行修复完成；MUST_FIX 0，纳入 SHOULD_FIX 1，已完成 1，PARTIAL/BLOCKED/FAILED 修复项 0。
- IMPLEMENTATION_STATUS / TEST_STATUS：本轮实现完成、Windows 专项 PASS；最终独立 Review 尚未重跑。
- PERFORMANCE_EVIDENCE_STATUS：未修改上传热路径，不宣称性能变化；RESOURCE_EVIDENCE_STATUS：Windows 实际卷查询通过，七牛大容量仍 NOT_VERIFIED。
- RELEASE_STATUS / EXTERNAL_GATE_STATUS：七牛目标环境仍 BLOCKED_EXTERNAL；不将容量工具修复视作云端上线验收通过。
- Completed：FIX-003。
- Open Actionable（执行器本轮范围）：无。
- Blocked Approval：无。
- Blocked External：七牛云端、CORS、跨日会话及 1/100 GiB；Unix/Windows 卷挂载专项环境实测未执行。
- Not Applicable：数据库迁移、重新打包消费、全解决方案/吞吐 Benchmark。
- Accepted Limitations：沿用既有跨系统初始化硬退出补偿边界，本轮未修改该流程。
- Verified Boundaries：缺失目录明确拒绝；原 Local/远端预算回归通过。
- Deferred：OPT-001 分片定点查询优化。
- No-Progress Check：CHANGED；Next Action：STOP；Goal Status：STOPPED_BLOCKED（仅剩独立验收/外部门禁，不自动 Fix）。
- Git：未 commit、push、PR 或执行真实业务迁移，保留无关修改。收口使用 task-finish --no-notify，未发送外部通知。
