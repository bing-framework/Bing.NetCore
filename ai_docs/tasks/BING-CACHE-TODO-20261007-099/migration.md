# Dead-letter Handle 存储迁移说明

## Redis 数据布局

旧版本把 JSON envelope 直接作为 `:dead` Sorted Set member。新版本把 SHA-256 handle 作为 member，并把原始 envelope 存到同一 outbox hash slot 的 `:dead-payloads` hash；`:sizes` 继续按 payload 字节数记录，`:bytes` 继续表示 payload 的 UTF-8 字节总数。缓存键命名空间和普通 pending/processing 队列格式不变。

`GetDeadLettersAsync` 每次只读取请求的有界条目。发现旧格式 member 时，会在 Lua 脚本中原子替换为 handle、保存 payload、保留原来的转移时间分数并迁移 size 字段；随后重新读取有界页，避免把迁移前已经合并的旧 member 和 handle 作为重复记录返回。新旧格式因并行写入而同时存在相同 payload 时，脚本会合并条目并修正字节计数。无需全库扫描或离线批量迁移。

字节预算仍只计算 JSON envelope 的 UTF-8 字节数，不包括 Redis hash、Sorted Set、键名或 allocator 内存。历史数据缺少计数辅助键时，已有状态读取和管理脚本会按 Redis 中的 payload 重建计数。

## API 调用迁移

管理服务读取条目后，只需把 `Handle` 和 `ConfirmationToken` 两个字符串交给后续请求；重放使用 `ReplayAsync(handle, actor)`，确认删除使用 `ConfirmDeleteAsync(handle, token, actor)`。原 DTO 重载保留，适合进程内已有调用方。handle 和 token 都应按敏感运维凭据处理，不写入普通日志。

## 部署顺序

新版本可以读取并惰性迁移旧 Redis 数据，但旧二进制不能解释新版本的 handle member 与 payload hash。切换共享 outbox 的存储格式时，必须确保旧二进制不再读取、清理或写入该 dead-letter 集合；建议通过蓝绿流量切换完成版本切换，并在切换期间暂停人工 dead-letter 操作。该跨版本并行运行场景未在本轮实机验证。
