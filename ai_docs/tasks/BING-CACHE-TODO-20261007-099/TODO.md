# 本轮 TODO

## 已完成

- [x] `IHybridInvalidationDeadLetterAdmin` 增加只依赖公开 handle/token 的重放和确认删除 API，保留 DTO 兼容重载。
- [x] Redis dead-letter 改用 handle member 与 payload hash；查看、重放、删除、清理和字节计数按同一命名空间原子维护。
- [x] 旧 JSON member 有界惰性迁移，并处理新旧版本并存时的重复 payload 和字节计数。
- [x] 管理操作先授权再按 handle 读取 payload；覆盖 JSON 往返、错误 handle、错误 token 和未授权不读 Redis。
- [x] 增加单节点 Redis 历史迁移/重复去重门禁和三主 Cluster 同槽、命名空间哨兵门禁。
- [x] 隔离 Redis 实测发现迁移后旧 member 与 handle 被同一页重复返回；读取路径改为迁移后重读有界页，单节点 net6.0/net8.0 各 11/11 通过。
- [x] 三主三从 Cluster 实测死信同槽脚本、旧格式迁移、重放、删除和哨兵隔离；net6.0/net8.0 各 1/1 通过。
- [x] 修正 Cluster 测试清理跨 slot 多键删除；EasyCaching.Tests 全量 net6.0/net8.0 各 133 通过、70 跳过，netstandard2.0 构建通过。
- [x] 更新 EasyCaching README、迁移说明和生产符号到测试映射。

## 未完成

- [ ] 在隔离蓝绿/滚动发布环境验证旧二进制停止访问 dead-letter 集合后，新版本读取并迁移旧数据；本轮未运行跨版本部署。
