# BING-CACHE-TODO-20261006-045：流式取消后的批次派发证据

## 目标

补齐 FreeRedis、CSRedis 和 EasyCaching 流式批量读取在“消费完成首批后取消”场景下的回归证据，确认取消会阻止下一批读取命令派发，同时保留已发送首批命令的事实边界。

## 实现范围

- EasyCaching 单元测试：`GetAllStreamAsync` 消费首批后取消时，Provider 批量读取调用次数保持为一次。
- FreeRedis 集成测试：`GetByPrefixStreamAsync` 消费首批后取消时，通过 Redis `MONITOR` 确认命名空间内 MGET 只执行一次。
- CSRedis 集成测试：同上。
- 保留已有的进入前取消、输入枚举停止和 `GetAllStreamAsync` MGET 监视测试；不修改生产流式实现。

## 验收

- EasyCaching 精准测试在 net6.0/net8.0 通过。
- FreeRedis/CSRedis 新增测试可在 net6.0/net8.0 编译；缺少 Redis 门控时明确跳过。
- 测试分别验证 Provider 调用或真实 Redis 命令，不能用输入枚举次数代替命令证据。
- 更新 `execution.md`、`TODO.md` 和生产符号到测试方法映射。

## 非目标

- 不宣称已验证真实集群、故障转移或网络层取消；这些仍需要外部 Redis 环境。
- 不调整流式 API、批大小、取消语义或性能预算。
