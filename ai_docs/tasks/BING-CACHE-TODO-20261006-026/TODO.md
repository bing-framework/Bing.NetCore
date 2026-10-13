# 本轮 TODO

## Open Actionable

- 无。

## Completed

- [x] 公共契约加入包含 `{}`、`*`、`?`、反斜线和 Unicode 的前缀流扫描，并排除相似前缀哨兵。
- [x] EasyCaching 增加首批成功、第二批异常的 `GetAllStreamAsync` 回归，验证已产出结果和原异常传播。
- [x] EasyCaching 双 TFM 单元测试通过；FreeRedis/CSRedis 公共契约项目完成编译并按 Redis 门控 Skip。

## Blocked External

- [ ] Redis MONITOR、完整 Cluster/分片/独立总线矩阵和严格历史三方基线仍需外部环境或历史源码。

## Inherited Open TODO

- [ ] 正式批量读/扫描 P95 与分配预算、100,000 项完整一致性和 T02/T03 后跨进程回源证据仍未闭环。

## Deferred

- [ ] 真实网络/连接池/重试指标、Hybrid 跨实例批量实机矩阵、流式服务端命令停止证据。
- [ ] Cluster/分片节点故障发生在流式第二批之后的实机异常传播矩阵。
