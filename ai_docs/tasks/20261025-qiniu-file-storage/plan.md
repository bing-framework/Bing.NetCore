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
