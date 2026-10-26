# 文件管理运维工具

执行前使用 appsettings.example.json 创建独立配置，配置 ConnectionStrings:FileManagement 和 FileManagement:Orm、Database、Stores。密钥用环境变量注入。schema 只导出 DDL，不要求对象存储凭据。七牛 Qiniu store 示例默认使用 ServerProxy；请填入 HTTPS Endpoint、对应 Region 和实际 S3 空间名，并参考[七牛部署说明](../../docs/operations/file-management/qiniu.md)。

| 命令 | 行为 |
|---|---|
| schema --config ... --output ... | 当前模型的 EF 初始 DDL / FreeSql 差异 DDL，永不自动执行或推断任意旧版本 |
| import --manifest ... --source ... --legacy-root ... --address-prefix ... | 默认预览；--apply 执行复制和稳定 GUID 映射 |
| migrate --file-id ... --target-store ... | 默认预览；--apply 排队迁移 |
| run-tasks --apply | 执行一批最多 20 个可领取持久任务 |
| operations / operation | 按范围查询任务，或读取指定任务与恢复审计 |
| retry-operation | 通过 expected version、operator、reason、request-id 恢复失败任务；需 --apply |
| batch-preview / batch-create | 预览候选或幂等创建迁移批次；写入需 --apply |
| batch-status / batch-items | 查看批次进度或按状态查询批次项 |
| batch-pause / batch-resume | 按批次版本暂停或恢复发现/入队；暂停期间已有任务继续对账，进度可更新，状态保持 Paused；需 --apply |
| batch-retry | 按版本和原因恢复失败批次项；需 --apply |
| verify --file-id ... | 回读文件计算 SHA-256 并校验长度/已存摘要 |
| local-cleanup --store ... | 预览旧临时文件；停止写入后 --offline true --apply |

所有命令都需要 --config，可用 --output 写 UTF-8 报告。涉及范围的运维命令必须显式提供 `--tenant-id` 或 `--host-scope true`；写操作默认只预览，需明确使用 `--apply`。导入清单每条须明确租户或 HostScope 及 OwnerId；工具不连接、推测或自动迁移真实业务数据，也不自动执行 DDL。已有基础文件管理 schema 的增强升级须使用匹配的[一次性数据库迁移脚本](../../docs/migrations/file-management-enhancements/README.md)，工具说明不代表能够自动升级任意旧 schema。完整部署、JSON 清单示例和故障恢复见[接入与迁移说明](../../docs/migrations/file-storage-adoption.md)，功能边界见[增强能力说明](../../docs/modules/file-storage-enhancements.md)，监控见[运维说明](../../docs/operations/file-management/README.md)。
