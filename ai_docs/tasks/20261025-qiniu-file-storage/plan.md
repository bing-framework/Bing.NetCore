# 七牛云文件存储实施计划

Task ID：20261025-qiniu-file-storage。用户批准日期：2026-10-25。

## 目标与固定决策

在现有文件管理体系中接入七牛云，支持上传、下载、删除、属性、Range、持久化分片续传、故障恢复、迁移，两套 Admin 和用户资料附件业务共用现有服务。

- 兼容优先：同时实现 ServerProxy 和 SignedParts，默认代理上传，直传通过真实七牛验证后由部署配置显式启用。
- 先实现、云端后验证：完成代码、本地测试和真实测试入口；缺少环境时将云端兼容性、1 GiB/100 GiB 容量验证单独记录为待完成。
- 复用 Bing.FileStorage.S3 和 AWSSDK.S3 4.0.104.6；新增 Bing.FileStorage.Qiniu，Provider net6.0，基础契约 netstandard2.0；测试 net6.0/net8.0。
- 使用私有空间和普通在线存储。CDN、转码、云端审核及归档解冻留待后续。
- 不自动提交、推送、创建 PR、发送外部消息或迁移真实业务数据；保留已有工作区修改。

## Provider 和契约

新增 AddBingQiniuStorage(name, configure) 与 BingQiniuStorageOptions，包含 Endpoint、Region、AccessKey、SecretKey、StorageNamespace、ContainerBuckets、UploadMode，以及兼容现有配置的 ForcePathStyle、ListPartsPageSize。

通用 S3 增加可选 ContainerBuckets，所有请求统一解析实际桶名，对象键保留逻辑容器。未配置映射的旧实例保持 Container=Bucket。七牛要求显式实际 S3 空间名、HTTPS Endpoint、对应 Region 和凭据。注册生成不可变快照；变更空间、区域或模式时使用新 StoreName。

S3UploadMode 包含 SignedParts、ServerProxy；通用 S3/MinIO 默认 SignedParts，七牛默认 ServerProxy。新增 ServerVerifiedParts、RequiresCompletionCleanup 能力标志；后者仅用于 Local 完成收尾。模式写入 Provider 会话元数据并持久化；恢复校验一致性，旧 S3 会话按 SignedParts 兼容，不静默切换模式。不伪造原子 CreateOnly 能力。

## 代理分片与恢复

1. 接收实际字节，单片临时文件暂存，计算 SHA-256 并验证客户端预登记摘要。
2. 使用明确长度、SigV4 载荷签名和 HTTPS 转发。ServerProxy 不依赖远端扩展分片 SHA-256；不关闭载荷签名来换取兼容。
3. 实际上传成功后保存回执：会话、序号、长度、SHA-256、远端 ETag。
4. 恢复和完成比对远端清单与可信回执；缺回执或 ETag/长度变化要求重传。不得把客户端预期摘要填入未经证实的远端结果。
5. 完成经 CAS 冻结后由持久任务执行；长度与会话元数据用于结果对账。分开处理 Local 的完成清单收尾和已完成 S3 UploadId 消失。

不新增数据库字段。复用 FileUploadPart.Sha256/ETag 和根 File.Version CAS。相同摘要重新登记保留回执；上传结束重新检查最新状态和期限；CAS 冲突时重新核对远端 ETag，不重放旧响应；冻结、中止、过期后不得保存回执。基础 Provider 不依赖数据库。策略同时覆盖 SaveAsync 和迁移写入。

SignedParts 保留现有受限分片签名以及远端 SHA-256 校验。客户端只能上传指定分片，不能签发任意 PutObject/Complete 权限。不自动降级校验或切换活跃会话。

## 下载与业务接入

应用鉴权下载、Range、短期 S3 GET 链接及公开 Ready 附件入口复用现有链路。两 Admin、Demo、迁移 Tool、容量 Probe 增加七牛注册和项目引用。复用用户资料权限、管理页面、调度、审计。迁移保持唯一目标键、全流摘要读回、位置 CAS 和旧副本延迟清理；默认实例切换只影响新文件。

## 阶段与验收

|阶段|目标和实施范围|依赖|验证与验收|
|---|---|---|---|
|QN-01|新增七牛测试项目、SDK 请求构造验证、真实环境门控|既有 SDK/接口|请求行为可验证；缺云环境明确 Skip|
|QN-02|新增 Provider/配置/映射/能力声明，统一所有 S3 请求桶解析|QN-01 测试骨架|编译、映射、范围隔离、旧 Provider 回归通过|
|QN-03|实现代理回执、恢复、完成收尾和显式直传|QN-02|两个 ORM SQLite 的回执竞争与完成恢复通过|
|QN-04|两 Admin、Demo、用户资料、迁移和容量工具接入|QN-02/03|上传、关联、下载、解绑、删除闭环；消费者构建通过|
|QN-05|部署说明、测试映射、打包消费和证据|前述阶段|本地交付完成；云端/容量门禁分开报告|

主代理负责接口、状态机、集成和最终验证；边界清晰的接入、测试和文档委派 luna_worker。必要实现偏差写入 execution.md。

## 测试与门禁

- Provider：空文件、非 Seek 流、长度错误、取消、流所有权、映射、租户隔离、幂等删除。
- 回执：重复登记、上传成功保存失败、旧响应竞争、完成冻结后写回、缺回执重传。
- 恢复：刷新、重启、分页、重复完成、迟到分片、中止、响应丢失、NoSuchUpload 对账。
- HTTP/权限：跨租户、越权、用户资料授权、中文文件名、Range、过期签名、浏览器 CORS。
- 迁移：摘要不符、数据库更新失败、并发删除、延迟清理、旧实例读取。
- 回归：Local 收尾、MinIO 强校验、两 ORM SQLite 实际 IO、两 Admin 装配。

按影响范围先直接测试，再受影响项目与集成；维护生产符号→测试方法映射，不以 Mock 代替真实云端证据。默认 100 GiB 上限、16 MiB 分片、浏览器并发4、分片签名15分钟、下载签名5分钟。七牛 S3 会话有效期在云端验收确认。

分别报告代码交付、云端验收、容量验收。配置缺失不阻塞本地实现，不能标记云端或容量 PASS。仅 OPEN_ACTIONABLE 触发修复循环，遵守 GOAL_RULES。

## 官方依据

- [S3 服务域名与空间名](https://developer.qiniu.com/kodo/4088/s3-access-domainname)
- [S3 API 兼容表](https://developer.qiniu.com/kodo/4087/compatible-s3-api)
- [兼容公共头，包括 x-amz-content-sha256](https://developer.qiniu.com/kodo/4091/s3-compatible-header)
- [.NET SDK 示例](https://developer.qiniu.com/kodo/12555/aws-sdk-csharp-examples)

官方示例不构成本仓库固定 AWS SDK v4 及目标七牛空间的已验证证据。

## 2026-10-26 增量：OPT-001 分片分页查询性能

### 目标与范围

OPT-001 已实施并由独立复审关闭。优化前代理上传每保存一片回执、每次 CAS 重试，都会调用 ListPartsAsync 枚举全部远端分页，只为找到一个分片；现已改为可选单片查询，以下为已完成的实施依据。

通过可选的单片查询能力，S3 使用排他游标 `part-number-marker = number - 1` 和 `max-parts = 1` 定位目标分片。每次 CAS 仍读取最新远端回执，不缓存 ETag。完整会话恢复和上传完成继续枚举并核对全清单。本轮不变更 HTTP 列表协议、数据库结构、Local 清单格式或上传校验策略。

### 设计决策

- 新增可选接口 IMultipartFilePartReader.GetPartAsync；不向现有 IMultipartFileStorage 添加必实现成员，保留第三方 Provider 源码兼容性。
- 提供 IMultipartFileStorage.GetPartAsync 扩展：优先使用可选能力，旧 Provider 回退到全量查询并精确匹配序号。只有分片缺失返回 null；上传会话缺失、权限、网络和取消保持原错误语义。
- 命名工厂包装器继续验证容器，再委托底层能力或兼容路径。
- S3 单片与完整枚举共用分页请求、结果映射和异常分类；严格核对返回序号、页大小和游标前进，缺失目标时不能把后续分片误认为目标。
- FileManagementService.SavePartReceiptAsync 接入单片查询，保留最新会话状态/期限、后端标识、长度、SHA-256、ETag 和根版本 CAS 校验。CAS 失败后重新查远端，不复用旧结果。

### 实施阶段与验收

|阶段|实施范围|验收|
|---|---|---|
|PQ-01|公共可选能力、兼容扩展、命名工厂装配|netstandard2.0 编译；旧 Provider 回退、取消、容器校验及边界测试|
|PQ-02|S3 精确分页查询、共用页面验证、业务回执接入|目标首片/尾片/缺失片、错误映射、异常分页、两 ORM SQLite 回执竞争及冻结回归|
|PQ-03|请求成本证据、受影响回归、消费者验证和文档|6400 片元数据场景对比全量枚举与单片查询的实际 SDK 请求/返回条目数；net6.0/net8.0 测试；真实 MinIO 门控；net6.0 包消费|

主代理负责实现和业务集成；luna_worker 负责独立的单片查询契约与 Provider 测试。新增测试仅覆盖真实行为和成本边界，不运行与本轮无关的容量压力测试。真实云端缺环境明确记为 NOT_VERIFIED，不将 SDK 替身计数当作吞吐、延迟或云端兼容证据。维护 execution.md、test-map.md 和 evidence/part-query-optimization；保留此前 review.md 为独立证据。

完成标准：主调用链确实只读取目标页，兼容旧 Provider，两套 ORM 的并发校验保持有效，影响范围测试通过，记录成本对比与环境限制，完成任务状态收口；不提交、不推送。

分页依据：[AWS S3 ListParts API](https://docs.aws.amazon.com/AmazonS3/latest/API/API_ListParts.html)。


## 2026-10-26 当前增量：OPT-002 / OPT-003 查询优化

用户已要求实施 review.md 列出的两项可选优化。此前 QN/PQ 阶段的代码交付保持有效；七牛云端、跨平台及容量验证继续独立门控。本轮只改变查询成本，不改变数据库 Schema、默认上传模式或物理存储流程。

### QS-01 轻量会话状态（OPT-002）

- 现状：uploader.js 的 waitForState 每秒调用完整 GetSessionAsync；Completing/Quarantined 也读取并返回全分片清单。
- 范围：FileManagementService、FileManagementControllerBase、Console/uploader.js、针对性业务/HTTP/浏览器测试。
- 设计：保留 GetSessionAsync(Guid,CancellationToken) 签名，增加显式 includeParts 重载；HTTP 添加默认 true 的查询参数，false 在原所有者/租户检查后直接返回现有元数据和空 Parts，不查询任何远端或数据库分片。false 的空数组不表示尚无已上传内容。
- 客户端：状态等待使用 false；真正进入上传/续传前获取一次完整清单并重新处理会话状态。轮询请求纳入现有 AbortController 集合，暂停/身份切换可取消；续传摘要循环观察暂停，不降低重新选文件的摘要检查。保持现有一秒轮询间隔，本轮不增加缓存或新状态机。
- 验证：两 ORM 的 6400 片会话，轻量查询不调用 ListPartsAsync、返回大小不随分片数量增长；原详情和实际续传仍包含已校验分片。HTTP 验证默认兼容、false、所有者及租户隔离；浏览器验证等待、刷新恢复、暂停与完成。
- 验收：主浏览器等待链实际使用轻量查询；不会把轻量空清单作为续传依据。

### QS-02 批量列表授权读取（OPT-003）

- 现状：ListAsync / SearchAsync 每个候选调用 AuthorizeReadAsync，两个 ORM 分别读取文件、会话、关联；每个候选额外三条 SELECT。
- 范围：新增 IFileReadBatchStore / FileReadSnapshot（仅 File + Bindings）；EF 与 FreeSQL 新增 partial 实现；业务列表接入共享授权判定；仓储及业务 SQL 计数测试。
- 契约：ReadReadyFilesAsync(scopeKey, fileIds, ct)，单批最多 100 个输入 ID；空批不读库，重复 ID 去重，数据库过滤 scope 和 Ready，返回脱离跟踪且同一短事务中的文件/关联快照，不加载 Session。结果不承诺顺序，业务按原候选顺序处理。
- 兼容：IFileManagementStore 不新增必实现成员；两个内置仓储实现可选接口，自定义仓储没有能力时继续逐项读取并执行同一授权。采用独立只读快照，避免把缺少 Session 的聚合误用于写入。
- 业务：从单文件读取授权提取统一判定（管理权限、本人未绑定、业务 Read）。列表每批加载快照，仍检查范围/Ready/创建者筛选；权限拒绝不占页容量，错误和取消继续传播，搜索游标按原候选推进。业务授权仍逐项调用，不加入跨请求权限缓存。
- 验证：两 ORM SQLite 的同范围/跨租户/非 Ready/缺失/重复/取消/快照隔离/上限；SQL 计数验证查询数按批次增长、不查 Session。业务验证旧仓储兼容、授权拒绝跨批、管理权限、候选读取后删除、游标和取消。以 50 条实际 SQLite 数据对比兼容路径与批量路径的 SELECT 数，不推断网络吞吐。
- 风险：批量快照语义必须保留安全边界；数据库短事务只覆盖读取，业务授权在事务外执行。

### QS-03 集成、证据和收口

- 依赖 QS-01/02 完成。主代理负责契约、业务/HTTP/浏览器集成、状态查询与最终验证；luna_worker 负责边界确定后的两 ORM 批量实现和直接仓储测试。先运行直接测试，再运行文件管理 net6/net8、两 Admin 文件相关测试和 Demo 构建；复用不变的 S3/Local Provider、MinIO 与大容量证据。
- 文档说明 includeParts 及可选批量契约；更新 execution.md/test-map.md，保留 review.md 独立证据。证据写入 evidence/query-optimizations；保留既有工作区修改，不提交、推送、通知或迁移数据。
- 完成标准：上述生产链路实际生效、既有授权/分页/续传语义通过回归、SQL/响应成本证据准确、必要构建通过，执行 task-finish --no-notify。外部云端验证缺失不阻塞本轮本地查询优化完成。
